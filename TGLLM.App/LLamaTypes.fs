module TGLLM.App.LLamaTypes

open System
open System.Collections.Concurrent
open System.Linq
open System.Threading
open FSharp.Control
open LLama
open LLama.Common
open LLama.Sampling
open Telegram.Bot
open Telegram.Bot.Types

type LLamaSessionState(model: LLamaWeights, llamaContext:LLamaContext, pipeline: BaseSamplingPipeline, chatSession:ChatSession, inferenceParams:InferenceParams, botName:string, userName:string) =
        static member Init (modelPath: string, botName:string, userName:string, systemMessage: string) =
            async {

                let modelPath = modelPath

                //LLama.Native.NativeLibraryConfig.LLama.WithLibrary("/")

                let parameters = ModelParams modelPath
                parameters.ContextSize <- 1024u

                let! model = LLamaWeights.LoadFromFileAsync parameters |> Async.AwaitTask
                let context = model.CreateContext parameters

                let executor = InteractiveExecutor context

                let chatHistory = ChatHistory()

                chatHistory.AddMessage(
                    AuthorRole.System,
                    systemMessage
                )

                let session = ChatSession(executor, chatHistory)

                let pipeLine = new DefaultSamplingPipeline()

                let inferenceParams = InferenceParams()
                inferenceParams.MaxTokens <- 256
                inferenceParams.AntiPrompts <- [ "User:" ]
                inferenceParams.SamplingPipeline <- pipeLine

                return new LLamaSessionState(model, context, pipeLine, session, inferenceParams, botName, userName)
            }
        
        member this.Model = model
        member this.LlamaContext = llamaContext
        member this.Pipeline = pipeline
        member this.ChatSession = chatSession
        member this.InferenceParams = inferenceParams
        member this.BotName = botName
        member this.UserName = userName
        
        interface IDisposable with
            member this.Dispose() =
                pipeline.Dispose()
                llamaContext.Dispose()
                model.Dispose()
                
        

type LLamaSession(llamaSessionState: LLamaSessionState) =
    member this.GetResponseFromLLamaAsync(userInput: string) =
        async {
            let response =
                llamaSessionState.ChatSession.ChatAsync(
                    ChatHistory.Message(AuthorRole.User, userInput),
                    llamaSessionState.InferenceParams
                )

            let! arr = response |> TaskSeq.toArrayAsync |> Async.AwaitTask

            let unitedResponse = String.Concat arr

            // to forget about userInput
            llamaSessionState.ChatSession.RemoveLastMessage() |> ignore
            llamaSessionState.ChatSession.RemoveLastMessage() |> ignore
            llamaSessionState.Pipeline.Reset()
            
            let startIdx = match unitedResponse.ToLower().IndexOf(llamaSessionState.BotName.ToLower()+": ") with | idx when idx = -1 -> 0 | idx -> idx+(llamaSessionState.BotName.Length + 2)
            let endIdx = match unitedResponse.ToLower().IndexOf(llamaSessionState.UserName.ToLower()+":") with | idx when idx = -1 -> unitedResponse.Length-1 | idx -> idx
            
            let reply = unitedResponse.Substring(startIdx, endIdx-startIdx)
            
            return reply
        }

type LLamaTgQueue(llamaSession: LLamaSession, tgBot: ITelegramBotClient) =
    let semaphore = new Semaphore(1, 1)
    let queue = ConcurrentQueue<Update>()

    member this.Enqueue(update: Update) =
        async {

            Console.WriteLine("new update " + update.Id.ToString() + ": " + update.Message.Text)

            if queue.Any(fun u -> u.Message.Chat.Id = update.Message.Chat.Id) then
                let! _ =
                    tgBot.SendMessage(
                        update.Message.Chat.Id,
                        "Ожидайте ответа на прошлый вопрос, потом повторите этот.",
                        replyParameters =
                            (let rp = ReplyParameters()
                             rp.ChatId <- update.Message.Chat.Id
                             rp.MessageId <- update.Message.Id
                             rp)
                    )
                    |> Async.AwaitTask

                ()
            elif queue.Count > 5 then
                let! _ =
                    tgBot.SendMessage(
                        update.Message.Chat.Id,
                        "Большие запросы! Повторите позднее.",
                        replyParameters =
                            (let rp = ReplyParameters()
                             rp.ChatId <- update.Message.Chat.Id
                             rp.MessageId <- update.Message.Id
                             rp)
                    )
                    |> Async.AwaitTask

                ()
            else
                queue.Enqueue update

                let! _ = tgBot.SendMessage(update.Message.Chat.Id, "В очереди") |> Async.AwaitTask

                let isFree = semaphore.WaitOne 0

                try
                    if not isFree then
                        Console.WriteLine("is busy, return " + update.Id.ToString())
                    else
                        Console.WriteLine("entered mutex " + update.Id.ToString())

                        let mutable continueLooping = true

                        while continueLooping do
                            let isDequeued, dequeuedUpdate = queue.TryDequeue()

                            if not isDequeued then
                                continueLooping <- false
                            else
                                do! this.HandleUpdate(dequeuedUpdate, tgBot)

                        Console.WriteLine("before release mutex in try " + update.Id.ToString())
                        semaphore.Release() |> ignore
                with ex ->
                    Console.WriteLine(ex.ToString())
                    Console.WriteLine("before release mutex in with " + update.Id.ToString())
                    semaphore.Release() |> ignore
        }

    member private this.HandleUpdate(update: Update, tgBot: ITelegramBotClient) =
        async {

            let! _ = tgBot.SendMessage(update.Message.Chat.Id, "Ожидание ответа") |> Async.AwaitTask

            Console.WriteLine("Wait for response from llm " + update.Id.ToString())
            let! response = llamaSession.GetResponseFromLLamaAsync ("User: " + update.Message.Text)

            Console.WriteLine("Recieved response form llm " + update.Id.ToString())

            let! _ =
                tgBot.SendMessage(
                    update.Message.Chat.Id,
                    response,
                    replyParameters =
                        (let rp = ReplyParameters()
                         rp.ChatId <- update.Message.Chat.Id
                         rp.MessageId <- update.Message.Id
                         rp)
                )
                |> Async.AwaitTask

            ()
        }

    interface IDisposable with
        member this.Dispose() = semaphore.Dispose()
