module TGLLM.App.Bot

open System
open System.Threading
open TGLLM.App.Types
open TGLLM.App.llama
open Telegram.Bot
open Telegram.Bot.Types

let onUpdate
    (botClient: ITelegramBotClient,
    update: Update,
    llamaTgQueue: LLamaTgQueue)= async {
    
    if String.IsNullOrEmpty(update.Message.Text) then
        let! sentMessage= botClient.SendMessage(update.Message.Chat.Id, "Сообщение пустое") |> Async.AwaitTask
        return ()
    
    let! sentMessage= botClient.SendMessage(update.Message.Chat.Id, "Принято") |> Async.AwaitTask
    
    llamaTgQueue.Enqueue(update) |> Async.Start
    
    return ()
}