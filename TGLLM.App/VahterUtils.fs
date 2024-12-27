module TGLLM.App.VahterUtils

(*
The code in this file is taken from:
https://github.com/fsharplang-ru/vahter-bot
MIT LICENSE
Copyright (c) 2023 Русскоязычное сообщество F#
*)

open System
open System.Text.Json
open System.Text.Json.Serialization

let botConfJsonOptions =
    let opts = JsonSerializerOptions(JsonSerializerDefaults.Web)
    opts.NumberHandling <- JsonNumberHandling.AllowReadingFromString
    opts
let fromJson<'a> (json: string) =
    JsonSerializer.Deserialize<'a>(json, botConfJsonOptions)

let inline (~%) x = ignore x

let getEnv name =
    let value = Environment.GetEnvironmentVariable name
    if value = null then
        ArgumentException $"Required environment variable %s{name} not found"
        |> raise
    else
        value