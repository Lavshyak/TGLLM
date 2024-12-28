open System
open System.Threading
open TGLLM.App.Bot
open TGLLM.App.Types
open TGLLM.App.VahterUtils
open TGLLM.App.llama
open FSharp.Control
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Telegram.Bot
open Telegram.Bot.Polling
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open System.Threading.Tasks

let botConfig: BotConfiguration =
    { ModelPath = getEnv "MODEL_PATH"
      BotToken = getEnv "BOT_TELEGRAM_TOKEN" }

let program =
    async {
        use! llamaSessionState = initLLamaSessionAsync (botConfig.ModelPath)

        let builderSettings = HostApplicationBuilderSettings()
        let builder = Host.CreateEmptyApplicationBuilder(builderSettings)

        %builder.Services
            .AddSingleton(botConfig)
            .ConfigureTelegramBot<Microsoft.AspNetCore.Http.Json.JsonOptions>(fun x -> x.SerializerOptions)
            .AddSingleton(llamaSessionState)
            .AddSingleton<LLamaSession>()
            .AddSingleton<LLamaTgQueue>()

        %builder.Services
            .AddHttpClient("telegram_bot_client")
            .AddTypedClient(fun httpClient sp ->
                let options = TelegramBotClientOptions(botConfig.BotToken)
                TelegramBotClient(options, httpClient) :> ITelegramBotClient)

        let app = builder.Build()

        let telegramClient = app.Services.GetRequiredService<ITelegramBotClient>()
        let llamaTgQueue = app.Services.GetRequiredService<LLamaTgQueue>()

        let pollingHandler =
            { new IUpdateHandler with
                member x.HandleUpdateAsync
                    (botClient: ITelegramBotClient, update: Update, cancellationToken: CancellationToken)
                    =
                    task {
                        if
                            not update.Message.IsFromOffline
                            && update.Message <> null
                            && update.Message.Type = MessageType.Text
                        then

                            let lastDate = DateTime.UtcNow.AddSeconds -5
                            let messageDate = update.Message.Date

                            if messageDate < lastDate then
                                printfn "пропущено старое сообщение"
                                return ()

                            Console.WriteLine("new update in handler: " + update.Message.Text)
                            //let ctx = app.Services.CreateScope()
                            //let client = ctx.ServiceProvider.GetRequiredService<ITelegramBotClient>()
                            do! onUpdate (botClient, update, llamaTgQueue)
                    }

                member this.HandleErrorAsync(botClient, ``exception``, source, cancellationToken) =
                    Console.WriteLine(``exception``.ToString())
                    Task.CompletedTask }

        do! telegramClient.DropPendingUpdates() |> Async.AwaitTask

        telegramClient.StartReceiving(pollingHandler, null, CancellationToken.None)

        do! app.WaitForShutdownAsync() |> Async.AwaitTask
    }

program |> Async.RunSynchronously
