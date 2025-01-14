module TGLLM.App.Types

open System
open LLama
open LLama.Common
open LLama.Sampling

[<CLIMutable>]
type BotConfiguration =
    {
        ModelPath:string        
        BotToken:string
    }

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
                
        