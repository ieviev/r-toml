open BenchmarkDotNet
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Order
open System.Text
open System.IO
open BenchmarkDotNet.Running
open System.Runtime.CompilerServices
open System.Collections.Generic

#nowarn "3391"

[<Orderer(SummaryOrderPolicy.FastestToSlowest)>]
[<MemoryDiagnoser>]
[<ShortRunJob>]
[<HideColumns("Gen0", "Gen1", "Gen2")>]
type Benchmarks() =
    member val Utf8: byte[] = null with get, set
    member val Utf16: string = null with get, set
    
    [<GlobalSetup>]
    member this.Setup() =
        let builder = new StringBuilder 2097152

        for i in 0..6..100_000 do
            builder.AppendLine $"key{i} = true" |> ignore
            builder.AppendLine $"key{i + 1} = false" |> ignore
            builder.AppendLine $"key{i + 2} = 'asd'" |> ignore
            builder.AppendLine $"key{i + 3} = 100" |> ignore
            builder.AppendLine $"key{i + 4} = 0.01" |> ignore
            builder.AppendLine $"key{i + 5} = '''tri'''" |> ignore

        this.Utf16 <- builder.ToString()
        this.Utf8 <- Encoding.UTF8.GetBytes this.Utf16
        ()
        

    [<Benchmark>]
    member this.CsToml() =
        CsToml.CsTomlSerializer.Deserialize<CsToml.TomlDocument> this.Utf8

    [<Benchmark>]
    member this.Tommy() =
        use reader = new StringReader(this.Utf16)
        Tommy.TOML.Parse reader

    [<Benchmark>]
    member this.Tomlet() =
        Tomlet.TomlParser().Parse this.Utf16

    [<Benchmark>]
    member this.TomlynUtf16() = Tomlyn.Toml.ToModel this.Utf16

    [<Benchmark>]
    member this.TomlynUtf8() = Tomlyn.Toml.Parse this.Utf8

    [<Benchmark>]
    member this.RTomlToDictionary() = RToml.toDictionary this.Utf8

    [<Benchmark>]
    member this.RTomlToArray() = RToml.toArray this.Utf8
    
    [<Benchmark>]
    member this.RTomlToStructArray() = RToml.toStructArray this.Utf8

    [<Benchmark>]
    member this.RTomlToValueList() =
        let pooled_list = RToml.toValueList this.Utf8
        pooled_list.Dispose()

    [<Benchmark>]
    member this.RTomlStream() =
        let mutable count = 0
        RToml.stream (
            this.Utf8,
            (fun _ v ->
                if v.kind = RToml.Token.TRUE then
                    count <- count + 1
            )
        )
        count


BenchmarkRunner.Run<Benchmarks>() |> ignore
