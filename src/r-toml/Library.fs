module RToml

#nowarn "FS3517"

open System
open System.Text
open System.Runtime.CompilerServices
open System.Collections.Generic

type Token =
    | NONE = 0uy
    | IGNORE = 1uy
    | COMMENT = 2uy
    | TABLE_STD = 3uy
    | TABLE_ARR = 4uy
    | UQ_KEY = 5uy
    | TRUE = 6uy
    | FALSE = 7uy
    | INT = 8uy
    | VALID_STR = 9uy
    | ESC_STR = 10uy
    | DATE = 11uy
    | FLOAT = 12uy
    | EMPTYSTR = 13uy
    | ARR1_INT = 14uy
    | ARR1_FLOAT = 15uy
    | ARR1_BOOL = 16uy
    | ARR1_STR = 17uy

[<Struct; NoComparison>]
type Value =
    [<DefaultValue>]
    val mutable kind: Token

    [<DefaultValue>]
    val mutable pos_begin: int

    [<DefaultValue>]
    val mutable pos_end: int

    override this.ToString() = $"{this.kind}[{this.pos_begin}..]"

    member inline this.ToBool() : bool =
        if not (this.kind = Token.TRUE || this.kind = Token.FALSE) then
            failwith $"invalid token kind: {this.kind}"

        this.kind = Token.TRUE

    member inline this.ToInt(data: ReadOnlySpan<byte>) : int =
        if this.kind <> Token.INT then
            failwith $"invalid token kind: {this.kind}"

        let slice = data.Slice(this.pos_begin, this.pos_end - this.pos_begin)
        System.Int32.Parse slice

    member inline this.ToString(data: ReadOnlySpan<byte>) : string =
        match this.kind with
        | Token.EMPTYSTR -> ""
        | Token.VALID_STR ->
            let slice = data.Slice(this.pos_begin, this.pos_end - this.pos_begin)
            System.Text.Encoding.UTF8.GetString slice
        | Token.ESC_STR -> failwith "this string needs to be escaped"
        | _ -> failwith $"invalid token kind: {this.kind}"

    member inline this.ToFloat(data: ReadOnlySpan<byte>) : float =
        if this.kind <> Token.FLOAT then
            failwith $"invalid token kind: {this.kind}"

        let slice = data.Slice(this.pos_begin, this.pos_end - this.pos_begin)
        System.Double.Parse slice

    member inline this.ToDateTimeOffset(data: ReadOnlySpan<byte>) : DateTimeOffset =
        if this.kind <> Token.DATE then
            failwith $"invalid token kind: {this.kind}"

        let slice = data.Slice(this.pos_begin, this.pos_end - this.pos_begin)
        // is there a way to do this without converting .net str
        System.DateTimeOffset.Parse(System.Text.Encoding.UTF8.GetString slice)



[<Struct; NoComparison>]
type Key =
    [<DefaultValue>]
    val mutable index: int

    [<DefaultValue>]
    val mutable root_begin: int

    [<DefaultValue>]
    val mutable root_end: int

    [<DefaultValue>]
    val mutable key_begin: int

    [<DefaultValue>]
    val mutable key_end: int

    override this.ToString() =
        if this.root_end = 0 then
            $"[{this.key_begin}..]"
        else if this.index = 0 then
            $"[{this.root_begin}..].[{this.key_begin}..]"
        else
            $"[{this.root_begin}..][{this.index}].[{this.key_begin}..]"

/// this is not really "internal" for compiler inlining purposes
module Internal =
    open System.Buffers

    /// memory pooled array
    /// supports enumeration with for loops or .AsSpan()
    [<Struct; NoComparison>]
    type ValueList<'t when 't: struct> =
        val mutable Size: int
        val mutable Limit: int
        val mutable Pool: 't array

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member this.Add item =
            if this.Size = this.Limit then
                this.GrowTo(this.Limit * 2)

            this.Pool[this.Size] <- item
            this.Size <- this.Size + 1

        member this.GrowTo newLimit =
            let newArray = ArrayPool.Shared.Rent newLimit
            Array.Copy(this.Pool, newArray, this.Size)
            ArrayPool.Shared.Return this.Pool
            this.Pool <- newArray
            this.Limit <- this.Pool.Length

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member this.Clear() = this.Size <- 0

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member this.GetEnumerator() =
            this.Pool.AsSpan(0, this.Size).GetEnumerator()

        member this.Length() = this.Size

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member inline this.AsSpan() = this.Pool.AsSpan(0, this.Size)

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member this.Dispose() =
            ArrayPool.Shared.Return(this.Pool, false)

        interface IDisposable with
            [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
            member this.Dispose() = this.Dispose()

        // these 2 are to prevent compiler making defensive copies in f#
        static member inline toSpan(vs: ValueList<'t>) = vs.Pool.AsSpan(0, vs.Size)

        static member inline add(vs: byref<ValueList<'t>>, v: 't) =
            if vs.Size = vs.Limit then
                vs.GrowTo(vs.Limit * 2)

            vs.Pool[vs.Size] <- v
            vs.Size <- vs.Size + 1

        new(initialSize: int) =
            {
                Size = 0
                Limit = initialSize
                Pool = ArrayPool.Shared.Rent initialSize
            }

    /// uses the end of the buffer to write the digit
    let writePosDecimal (chars: Span<char>) (i: int) =
        assert (i > 0)
        let mutable q = i
        let mutable pos = chars.Length - 1
        let mutable rem = 0

        while q <> 0 do
            q <- Math.DivRem(q, 10, &rem)
            chars[pos] <- char (rem + 48)
            pos <- pos - 1

        chars.Slice(pos + 1)

    // this should ideally be a transducer but
    // but it's still better than default interpolation
    let keyToChars
        (
            encoding: UTF8Encoding,
            key_buffer: byref<ValueList<char>>,
            key: Key,
            bytes: ReadOnlySpan<byte>
        ) : Span<char> =

        let len_u8 = (key.root_end - key.root_begin) + (key.key_end - key.key_begin)

        if len_u8 + 22 >= key_buffer.Limit then
            key_buffer.GrowTo(len_u8 + 22) // 22: 10 digits + 10 digits + . + .

        let mutable numchars = 0
        let bufspan = key_buffer.Pool.AsSpan()
        let key_s = bytes.Slice(key.key_begin, key.key_end - key.key_begin)
        // key only
        if key.root_end = 0 then
            encoding.TryGetChars(key_s, bufspan, &numchars) |> ignore
            bufspan.Slice(0, numchars)
        // root.key
        else
            let root = bytes.Slice(key.root_begin, key.root_end - key.root_begin)
            encoding.TryGetChars(root, bufspan, &numchars) |> ignore

            if key.index > 0 then
                bufspan[numchars] <- '/'
                let intchars = writePosDecimal bufspan key.index
                intchars.CopyTo(bufspan.Slice(numchars + 1))
                numchars <- numchars + 1 + intchars.Length

            bufspan[numchars] <- '.'
            let mutable numchars2 = 0
            encoding.TryGetChars(key_s, bufspan.Slice(numchars + 1), &numchars2) |> ignore
            let mutable end1 = numchars + numchars2 + 1
            bufspan.Slice(0, end1)


    let inline onToken
        (key: byref<Key>)
        (prev: int)
        (nextpos: int)
        (tag: Token)
        ([<InlineIfLambda>] lambda: Value -> unit)
        =
        match tag with
        | Token.UQ_KEY ->
            key.key_begin <- prev
            key.key_end <- nextpos
        | Token.TABLE_STD ->
            key.index <- 0
            key.root_begin <- prev
            key.root_end <- nextpos
        | Token.TABLE_ARR ->
            key.index <- key.index + 1
            key.root_begin <- prev
            key.root_end <- nextpos
        | _ -> lambda (Value(kind = tag, pos_begin = prev, pos_end = nextpos))


type Key with
    /// key struct to dot separated string
    member this.ToString(bytes: ReadOnlySpan<byte>) =
        let encoding =
            UTF8Encoding(
                encoderShouldEmitUTF8Identifier = false,
                throwOnInvalidBytes = true
            )

        use mutable key_buffer = new Internal.ValueList<char> 128
        String(Internal.keyToChars (encoding, &key_buffer, this, bytes))

let inline stream
    (data: ReadOnlySpan<byte>, [<InlineIfLambda>] on_key_value: Key -> Value -> unit)
    =
    let mutable currentKey = Key()

    Automata.DFA.lex
        (fun pos1 pos2 tag ->
            Internal.onToken
                &currentKey
                pos1
                pos2
                (LanguagePrimitives.EnumOfValue tag)
                (fun v -> on_key_value currentKey v)
        )
        Automata.toml
        data

/// IMPORTANT: dispose the ValueList after use
let inline toValueList (data: ReadOnlySpan<byte>) : Internal.ValueList<_> =
    let mutable d = new Internal.ValueList<KeyValuePair<Key, Value>> 512
    stream (data, (fun k v -> Internal.ValueList.add (&d, KeyValuePair(k, v))))
    d

let inline toStructArray (data: ReadOnlySpan<byte>) =
    let mutable vlist = toValueList data
    let arr = vlist.AsSpan().ToArray()
    vlist.Dispose()
    arr

let inline toArray (data: byte[]) =
    let mutable tmp = new Internal.ValueList<KeyValuePair<_, Value>> 512

    let encoder =
        UTF8Encoding(encoderShouldEmitUTF8Identifier = false, throwOnInvalidBytes = true)

    let mutable key_buffer = new Internal.ValueList<char> 128

    stream (
        ReadOnlySpan<byte>.op_Implicit data,
        (fun k v ->
            Internal.ValueList.add (
                &tmp,
                KeyValuePair(
                    String(
                        Internal.keyToChars (
                            encoder,
                            &key_buffer,
                            k,
                            ReadOnlySpan<byte>.op_Implicit data
                        )
                    ),
                    v
                )
            )
        )
    )

    let arr = tmp.AsSpan().ToArray()
    tmp.Dispose()
    key_buffer.Dispose()
    arr

let inline toDictionary (data: ReadOnlySpan<byte>) =
    let mutable tmp = new Internal.ValueList<KeyValuePair<Key, Value>> 512

    stream (data, (fun k v -> Internal.ValueList.add (&tmp, KeyValuePair(k, v))))

    let encoder =
        UTF8Encoding(encoderShouldEmitUTF8Identifier = false, throwOnInvalidBytes = true)

    let mutable key_buffer = new Internal.ValueList<char> 128

    let dictionary = Dictionary()

    for entry in Internal.ValueList.toSpan tmp do
        dictionary.Add(
            String(Internal.keyToChars (encoder, &key_buffer, entry.Key, data)),
            entry.Value
        )
    // avoid implicit try with block
    tmp.Dispose()
    key_buffer.Dispose()
    dictionary
