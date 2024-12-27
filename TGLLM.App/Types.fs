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
    
type LLamaSessionState(model: LLamaWeights, llamaContext:LLamaContext, pipeline: BaseSamplingPipeline, chatSession:ChatSession, inferenceParams:InferenceParams) =
        let mutable disposed: bool = false
        
        member this.Model = model
        member this.LlamaContext = llamaContext
        member this.Pipeline = pipeline
        member this.ChatSession = chatSession
        member this.InferenceParams = inferenceParams
        
        interface IDisposable with
            member this.Dispose() =
                if not disposed then
                    disposed <- true
                    pipeline.Dispose()
                    llamaContext.Dispose()
                    model.Dispose()