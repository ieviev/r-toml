open System.Text
open Expecto
open System

let sample1 =
        "# some comment

[person]

boolean = true
boolean2 = false
int = 1
float = 0.005
str_basic_e = \"\"
str_lit_e = ''
str_basic = \"basic string\"
str_basic_esc = \"contains\\n escape\"
str_lit_esc = 'afdf\\nsd'
str_lit = 'literal string'
str_lit_ml = '''
ml literal string
'''
dateoffset = 1979-05-27T07:32:00Z
int_arr = [1, 2, 3]
str_arr = [\"a\", \"b\", \"c\"]
float_arr = [0.1, 0.5, 0.6]
bool_arr = [true, false, true]


[[children]]
id = 1
name = \"aaaa\"

[[children]]
id = 2
name = \"bbbb\"
"B

let utf8 (b:System.Span<byte>) = System.Text.Encoding.UTF8.GetString(b)

let testRoot =
    testList "root" [
        test "bad toml 1" {
            Expect.throws
                (fun _ -> RToml.Parse.toDictionary ("[server]]\na = 1"B) |> ignore)
                "invalid toml should throw"
        }
        test "bad toml 2" {
            Expect.throws
                (fun _ -> RToml.Parse.toDictionary ("[serv/er]]\na = 1"B) |> ignore)
                "invalid toml should throw"
        }
        test "ok toml 1" {
            let data = "[server]\na = 1"B
            let d = RToml.Parse.toDictionary "[server]\na = 1"B
            Expect.equal (d["server.a"].ToInt(data)) 1 "not equal"
        }
        test "all cases" {
            let d = RToml.Parse.toDictionary sample1
            let eq key fn v = Expect.equal (fn (d[key])) v "not equal"
            eq "person.boolean" (fun v -> v.ToBool()) true
            eq "person.boolean2" (fun v -> v.ToBool()) false
            eq "person.int" (fun v -> v.ToInt sample1) 1
            eq "person.float" (fun v -> v.ToFloat sample1) 0.005
            eq "person.str_basic_e" (fun v -> v.ToStr(sample1)) ""
            eq "person.str_basic_esc" (fun v -> v.kind) RToml.Token.ESC_STR
            eq "person.str_lit_esc" (fun v -> v.ToStr(sample1)) """afdf\nsd"""
            eq "person.str_lit" (fun v -> v.ToStr(sample1)) """literal string"""
            eq "person.str_lit_ml" (fun v -> v.ToStr(sample1)) "ml literal string\n"
            eq "person.dateoffset" (fun v -> v.ToDateTimeOffset(sample1)) (System.DateTimeOffset.Parse("1979-05-27T07:32:00Z"))
            eq "person.int_arr" (fun v -> utf8 (sample1.AsSpan(v.pos_begin,v.pos_end - v.pos_begin))) "1, 2, 3"
            eq "person.str_arr" (fun v -> utf8 (sample1.AsSpan(v.pos_begin,v.pos_end - v.pos_begin))) "\"a\", \"b\", \"c\""
            eq "person.float_arr" (fun v -> utf8 (sample1.AsSpan(v.pos_begin,v.pos_end - v.pos_begin))) "0.1, 0.5, 0.6"
            eq "person.bool_arr" (fun v -> utf8 (sample1.AsSpan(v.pos_begin,v.pos_end - v.pos_begin))) "true, false, true"
            eq "children/1.id" (fun v -> v.ToInt sample1) 1
            eq "children/1.name" (fun v -> v.ToStr sample1) "aaaa"
            eq "children/2.id" (fun v -> v.ToInt sample1) 2
            eq "children/2.name" (fun v -> v.ToStr sample1) "bbbb"
        }
    ]



[<EntryPoint>]
let main argv =
    Expecto.Tests.runTestsWithCLIArgs [] argv testRoot
