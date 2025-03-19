open System.Text
// For more information see https://aka.ms/fsharp-console-apps

let sample1 = System.Text.Encoding.UTF8.GetBytes """# some comment

[person]

[person.info.name]

boolean = true
boolean2 = false
int = 1
float = 0.005
str_basic_e = ""
str_lit_e = ''
str_basic = "basic string"
str_basic_esc = "contains\n escape"
str_lit_esc = 'afdf\nsd'
str_lit = 'literal string'
str_lit_ml = '''
ml literal string
'''
dateoffset = 1979-05-27T07:32:00Z
int_arr = [1, 2, 3]
str_arr = ["a", "b", "c"]
float_arr = [0.1, 0.5, 0.6]
bool_arr = [true, false, true]


[[children]]
id = 1
name = "aaaa"

[[children]]
id = 2
name = "bbbb"
"""

// let sample = System.Text.Encoding.UTF8.GetBytes """# some comment

// [person]

// [person.info.name]

// boolean = true
// boolean2 = false
// int = 1
// float = 0.005
// str_basic_e = ""
// str_lit_e = ''

// """

// let sample = System.Text.Encoding.UTF8.GetBytes """key = 123"""
let builder = new StringBuilder 2097152

for i in 0..6..100_000 do
    builder.AppendLine $"key{i} = true" |> ignore
    builder.AppendLine $"key{i + 1} = false" |> ignore
    builder.AppendLine $"key{i + 2} = 'asd'" |> ignore
    builder.AppendLine $"key{i + 3} = 100" |> ignore
    builder.AppendLine $"key{i + 4} = 0.01" |> ignore
    builder.AppendLine $"key{i + 5} = '''tri'''" |> ignore

let sample =  Encoding.UTF8.GetBytes (builder.ToString())

// let long = System.Text.Encoding.UTF8.GetBytes(builder.ToString())
// open System.Runtime.CompilerServices

let mutable count = 0

// // let d = RToml.Parse.toDictionary(sample)
// // let d = RToml.Parse.toKeyDictionary(sample)
// // let d = RToml.Parse.toArray(sample)
// // let d = RToml.Parse.toKeyDictionary(sample)
// let d = RToml.Parse.toDictionary(sample)
// for v in sample do 
//     stdout.WriteLine $"%A{v}"

// stdout.WriteLine $"result:%A{toml}"
