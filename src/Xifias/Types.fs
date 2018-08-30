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
    type Response =
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
        | RespondNow of Response
        | RespondAfter of handler:(HttpContext -> Response)
        | RespondAfterAsync of handler:(HttpContext -> Async<Response>)
        | RespondAfterTask of handler:(HttpContext -> Task<Response>)


    type RouteContext =
        {
            HttpContext : HttpContext
            Handler : ResponseHandler option
        }