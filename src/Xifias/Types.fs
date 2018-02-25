namespace Xifias

[<AutoOpen>]
module Types =

    open System.Threading.Tasks
    open Microsoft.AspNetCore.Http


    type ResponseBody =
        | SetBodyStream of System.IO.Stream
        | WriteBodyStreamAsync of (System.IO.Stream -> Async<unit>)
        | WriteBodyStreamTask of (System.IO.Stream -> Task)


    // there really aren't that many pieces to an HTTP response
    type RouteResponse =
        {
            StatusCode : int
            StatusMessage : string option
            Headers : (string * obj) list
            Body : ResponseBody option
        }
            with
                static member ok =
                    {
                        StatusCode = 200
                        StatusMessage = None
                        Headers = []
                        Body = None
                    }


    type ResponseHandler =
        | RespondNow of RouteResponse
        | RespondAfter of handler:(HttpContext -> RouteResponse)
        | RespondAfterAsync of handler:(HttpContext -> Async<RouteResponse>)
        | RespondAfterTask of handler:(HttpContext -> Task<RouteResponse>)


    type RouteContext =
        {
            HttpContext : HttpContext
            Handler : ResponseHandler option
        }
