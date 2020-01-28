namespace Xifias

module Middleware =

    open Microsoft.AspNetCore.Http


    // Request was not handled, but I was required to handle it
    let noRoute =
        Responses.response [
            Responses.statusCodeInt 501
            Responses.header "Route-Info" "\"no matching route\""
        ]


    let useRoute (route: HttpContext -> Handling option) : RequestDelegate -> RequestDelegate =
        // delegate wrangling to make aspnet happy
        fun (next: RequestDelegate) ->
            RequestDelegate (
                fun (context: HttpContext) ->
                    // None of the routes matched, so pass the request down the line.
                    route context
                    |> Option.map (ResponseMutation.setResponse context)
                    |> Option.defaultWith (fun () -> next.Invoke context)
            )


    let runRoute (route: HttpContext -> Handling option) : RequestDelegate =
        RequestDelegate (
            fun (context: HttpContext) ->
                // I am the end of the line. It is my duty to respond.
                route context
                |> Option.defaultWith (fun _ -> HandleSync noRoute)
                |> ResponseMutation.setResponse context
        )


    let runFn (f: HttpContext -> Handling) : RequestDelegate =
        RequestDelegate (
            fun (context: HttpContext) ->
                f context
                |> ResponseMutation.fromResponse context
        )


module Extensions =

    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Http


    type IApplicationBuilder with

        member this.UseXifias (route: HttpContext -> Handling option) =
            this.Use(Middleware.useRoute route)

        member this.RunXifias (route: HttpContext -> Handling option) =
            this.Run(Middleware.runRoute route)

        member this.RunFn (f: HttpContext -> Handling) =
            this.Run(Middleware.runFn f)