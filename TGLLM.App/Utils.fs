module TGLLM.App.Utils

(*
Some of the code in this file is taken from:
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
        
let notNullOrGetEnv (value:string) (envName:string) :string = 
    if value <> null then
        value
    else
        getEnv envName

let notNullOr (value:'a) (onNull:'a) =
    if value <> null then
        value
    else
        onNull

let notNullOrCalculate (value:'a) (onNull:unit->'a) =
    if value <> null then
        value
    else
        onNull()