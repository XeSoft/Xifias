namespace Xifias

[<AutoOpen>]
module Types =

    open System.Threading.Tasks


    type ResponseBody =
        | SetBodyStream of System.IO.Stream
        | WriteBodyStreamAsync of (System.IO.Stream -> Async<unit>)
        | WriteBodyStreamTask of (System.IO.Stream -> Task)


    type Response =
        {
            StatusCode: int
            StatusMessage: string option
            Headers: (string * obj) list
            Body: ResponseBody option
        }
            with
                static member ok =
                    {
                        StatusCode = 200
                        StatusMessage = None
                        Headers = []
                        Body = None
                    }


    type ResponseType =
        | ManualResponse
        | Response of Response


    type Handling =
        | HandleSync of ResponseType
        | HandleAsync of Async<ResponseType>
        | HandleTask of Task<ResponseType>