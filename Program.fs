module serialization.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open FsToolkit.ErrorHandling


// ---------------------------------
// Models
// ---------------------------------

module Domain =
    type Tone =
        | Formal
        | Casual

    type Greeting = { Addressee: string; Tone: Tone }

    module Greeting =

        let message greeting =
            match greeting.Tone with
            | Formal -> sprintf "Salutations, %s. With the highest respect, Giraffe" greeting.Addressee
            | Casual -> sprintf "Hello %s, from Giraffe!" greeting.Addressee

module Dto =
    type Greeting = { Addressee: string; Tone: string }

    let toneFromString =
        function
        | "Formal" -> Ok Domain.Tone.Formal
        | "Casual" -> Ok Domain.Tone.Casual
        | unknown -> Error $"Unknown tone {unknown}"

    let validateAddressee addressee =
        if addressee |> String.IsNullOrWhiteSpace then
            Error "Missing addressee"
        else
            Ok addressee

    let greetingFromDto dto =
        validation {
            let! tone = toneFromString dto.Tone
            and! addressee = validateAddressee dto.Addressee

            return
                { Domain.Addressee = addressee
                  Domain.Tone = tone }
        }

open Domain

let greetingHandler next (ctx: HttpContext) =
    task {
        let! greeting = ctx.BindJsonAsync<Dto.Greeting>()

        match Dto.greetingFromDto greeting with
        | Ok greeting ->
            let message = Greeting.message greeting

            return! text message next ctx
        | Error e ->
            let errorMessage = String.concat "; " e
            return! (setStatusCode 400 >=> text errorMessage) next ctx
    }

let webApp =
    choose
        [ POST >=> choose [ route "/" >=> greetingHandler ]
          setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder: CorsPolicyBuilder) =
    builder
        .WithOrigins("http://localhost:5000", "https://localhost:5001")
        .AllowAnyMethod()
        .AllowAnyHeader()
    |> ignore

let configureApp (app: IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()

    (match env.IsDevelopment() with
     | true -> app.UseDeveloperExceptionPage()
     | false -> app.UseGiraffeErrorHandler(errorHandler).UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services: IServiceCollection) =
    services.AddCors() |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder: ILoggingBuilder) =
    builder.AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot = Path.Combine(contentRoot, "WebRoot")

    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseContentRoot(contentRoot)
                .UseWebRoot(webRoot)
                .Configure(Action<IApplicationBuilder> configureApp)
                .ConfigureServices(configureServices)
                .ConfigureLogging(configureLogging)
            |> ignore)
        .Build()
        .Run()

    0