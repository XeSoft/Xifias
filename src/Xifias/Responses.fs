namespace Xifias

//[<AutoOpen>]
module Responses =

    open System.IO
    open System.Text
    open Types
    open Microsoft.Extensions.Primitives
    open Microsoft.Net.Http.Headers


    module Internal =

        let setBody body (response : RouteResponse) =
            { response with Body = Some body }


    /// <summary>Create a response with the provided list of response options.</summary>
    /// <remarks>Example: <c>response [ statusCode 200 ]</c>
    let response (changes : (RouteResponse -> RouteResponse) list) =
        List.fold (fun response f -> f response) RouteResponse.ok changes


    /// Set the status code of the response.
    /// Example: <c>response [ statusCode 200 ]</c>
    let statusCode (statusCode : int) (response : RouteResponse) =
        { response with StatusCode = statusCode }


    /// <summary>Set the status message of the response.
    /// Example: <c>response [ statusCode 200; statusMessage "okie dokie" ]</c></summary>
    /// <remarks>NOTE: You should probably not depend on a custom Status Message aka Reason Phrase to convey extra information. It is commonly ignored by browsers, for example.</remarks>
    let statusMessage (reason : string) (response : RouteResponse) =
        { response with StatusMessage = Some reason }


    /// <summary>Set a header in the response.
    /// Example: <c>response [ statusCode 200; header "My-Header" "HeaderValue" ]</c></summary>
    /// <remarks>Setting the same header multiple times will overwrite the previous header.</remarks>
    let header (key : string) (value : obj) (response : RouteResponse) =
        { response with Headers = (key, value) :: response.Headers }


    /// <summary>Set the response to be a temporary redirect to the provided location.</summary>
    /// <remarks>This sets the status code to 302, and sets the Location header.</remarks>
    let redirect (location : string) =
        statusCode 302
            >> header HeaderNames.Location location


    /// <summary>Set the response to be a permanent redirect to the provided location.</summary>
    /// <remarks>This sets the status code to 301, and sets the Location header.</remarks>
    let redirectPermanent (location : string) =
        statusCode 301
            >> header HeaderNames.Location location


    /// <summary>Set the response to be an attachment with the given filename.</summary>
    /// <remarks>
    /// In most browsers, this will bring up the Save As dialog. But only when the user directly clicks a link or submits a form to trigger this response.
    /// There is no such prompt when the response is send back via a Javascript XmlHttpRequest. Instead you may need to use the Javascript Blob API or some other method.
    /// See https://stackoverflow.com/questions/16086162/handle-file-download-from-ajax-post/23797348#23797348
    /// </remarks>
    let asAttachment (filename : string) =
        header HeaderNames.ContentDisposition (sprintf "attachment; filename=\"%s\"" filename)


    /// <summary>Write a stream to the response body.</summary>
    /// <remarks>
    /// This will send a chunked response since the length of the stream is not known.
    /// If the stream is write-only or closed, the response will return a 501 with a note in the headers.
    /// </remarks>
    let bodyStream (stream : Stream) (response : RouteResponse) =
        Internal.setBody (SetBodyStream stream) response


    /// Write binary data to the response body.
    let bodyBytes (bytes : byte array) =
        header HeaderNames.ContentLength (StringValues(bytes.Length.ToString()))
            >> bodyStream (new MemoryStream(bytes))


    /// Write a string to the response body.
    let bodyString (s : string) (response : RouteResponse) =
        bodyBytes (Encoding.UTF8.GetBytes s) response


    /// <summary>Provide an Async-returning function which will write to the response body.</summary>
    /// <remarks>
    /// This will send a chunked response since the length of the stream is not known.
    /// This is appropriate to use when you need to send a large response, such as a file.
    /// </remarks>
    let bodyWriterAsync (writerAsync : Stream -> Async<unit>) (response : RouteResponse) =
        Internal.setBody (WriteBodyStreamAsync writerAsync) response


    /// <summary>Provide a Task-returning function which will write to the response body.</summary>
    /// <remarks>
    /// This will send a chunked response since the length of the stream is not known.
    /// This is appropriate to use when you need to send a large response, such as a file.
    /// </remarks>
    let bodyWriterTask (writerTask : Stream -> System.Threading.Tasks.Task) (response : RouteResponse) =
        Internal.setBody (WriteBodyStreamTask writerTask) response


    /// Set the content type for the response.
    let contentType (mediaType : string) =
        header HeaderNames.ContentType mediaType
 

    /// Write a string to the response body and set the content type to "text/plain".
    let contentText (bodyText : string) =
        contentType "text/plain"
            >> bodyString bodyText


    /// Write a string to the response body and set the content type to "application/json".
    let contentJson (bodyJson : string) =
        contentType "application/json"
            >> bodyString bodyJson

