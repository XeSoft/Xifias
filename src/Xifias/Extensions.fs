namespace Xifias

module Middleware =

    open Microsoft.AspNetCore.Http


    let createRequest (context : HttpContext) =
        {
            HttpContext = context
            Handler = None
        }


    // Request was not handled, but I was required to handle it
    let noRouteResponse =
        Responses.response [
            Responses.statusCode 501
            Responses.header "Xiphias-Info" "\"no matching route\""
        ]
            

    let useRoute (route : Routing.RouteHandler) : RequestDelegate -> RequestDelegate =
        // delegate wrangling to make aspnet happy
        fun (next : RequestDelegate) ->
            RequestDelegate (
                fun (context : HttpContext) ->
                    let requestContext = createRequest context

                    // None of the routes matched, so pass the request down the line.
                    route requestContext
                        |> Option.map ResponseMutation.setResponse
                        |> Option.defaultWith (fun () -> next.Invoke context)
            )


    let runRoute (route : Routing.RouteHandler) : RequestDelegate =
        RequestDelegate (
            fun (context : HttpContext) ->
                let requestContext = createRequest context

                // I am the end of the line. It is my duty to respond.
                route requestContext
                    |> Option.defaultValue { requestContext with Handler = Some (RespondNow noRouteResponse) }
                    |> ResponseMutation.setResponse
        )


module Extensions =

    open Microsoft.AspNetCore.Builder


    type IApplicationBuilder with

        member this.UseXifias (route : Routing.RouteHandler) =
            this.Use(Middleware.useRoute route)

        member this.RunXifias (route : Routing.RouteHandler) =
            this.Run(Middleware.runRoute route)
