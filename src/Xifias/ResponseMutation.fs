namespace Xifias

module ResponseMutation =

    open System.IO
    open System.Threading.Tasks
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Http.Features
    open Microsoft.Extensions.Primitives
    open FSharp.Control.Tasks.ContextInsensitive
    open Types


    module List =
        
        let iterReverse f list =
            List.foldBack (fun item () -> f item) list ()


    module Mutate =
        
        let statusCode i (response : HttpResponse) =
            response.StatusCode <- i


        let statusMessage s (response : HttpResponse) =
            response
                .HttpContext
                .Features
                .Get<IHttpResponseFeature>()
                .ReasonPhrase <- s


        let addHeader key value (response : HttpResponse) =
            response.Headers.[key] <- StringValues(value.ToString())


        (*
        
            I chased down the Kestrel source code (shown below) to make sure I don't have to flush the stream.
            Confirmed: The stream is automatically flushed on both HTTP 1 and HTTP 2 protocols.

            ======= How I know the HttpResponse.Body is HttpResponseStream =======

            First, HttpResponseStream is descended from Stream like this HttpResponseStream :> WriteOnlyStream :> Stream
                ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Http/HttpResponseStream.cs
                ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Infrastructure/WriteOnlyStream.cs
            The Streams class creates a new HttpResponseStream when created and returns it when started
                ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Infrastructure/Streams.cs
                The HttpProtocol sets its ResponseBody property from Streams.Start
                    ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Http/HttpProtocol.cs
                    HttpProtocol is setup as IFeatureCollection and implements IHttpResponseFeature in a partial class
                        IHttpResponseFeature.Body is returned from HttpProtocol.ResponseBody
                        ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Http/HttpProtocol.FeatureCollection.cs
                    HttpProtocol passes itself to IHttpApplication<T>.CreateContext (!!!)
                        IHttpApplication<T> appears to have one impl: HostingApplication
                        ref https://github.com/aspnet/Hosting/blob/release/2.1/src/Microsoft.AspNetCore.Hosting/Internal/HostingApplication.cs
                            HostingApplication.CreateContext calls IHttpContextFactory.Create with IFeatureCollection
                                IHttpContextFactory appears to have one impl: HttpContextFactory
                                ref https://github.com/aspnet/HttpAbstractions/blob/release/2.1/src/Microsoft.AspNetCore.Http/HttpContextFactory.cs
                                    HttpContextFactory.Create calls new DefaultHttpContext with IFeatureCollection
                                        DefaultHttpContext wraps the IFeatureCollection in FeatureReferences
                                        ref https://github.com/aspnet/HttpAbstractions/blob/release/2.1/src/Microsoft.AspNetCore.Http/DefaultHttpContext.cs
                                        ref https://github.com/aspnet/HttpAbstractions/blob/release/2.1/src/Microsoft.AspNetCore.Http.Features/FeatureReferences.cs
                                        DefaultHttpContext sets its Response from InitializeHttpResponse which calls new DefaultHttpResponse(this) (!!!)
                                        ref https://github.com/aspnet/HttpAbstractions/blob/release/2.1/src/Microsoft.AspNetCore.Http/Internal/DefaultHttpResponse.cs
                                            DefaultHttpResponse wraps IFeatureCollection in yet another FeatureReferences
                                            DefaultHttpResponse uses the wrapped IFeatureCollection's IHttpResponseFeature.Body as its Body


            ======= Tracing FlushAsync path =======

            HttpResponseStream.FlushAsync (and Flush too) calls IHttpResponseControl.FlushAsync
                IHttpResponseControl in this case is HttpProtocol
                ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Http/HttpProtocol.cs

                HttpProtocol.FlushAsync calls IHttpOutputProducer.FlushAsync
                    IHttpOutputProducer can be Http1OutputProducer or Http2OutputProducer
                    ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Http/Http1OutputProducer.cs
                    ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Http2/Http2OutputProducer.cs

        --->        Http1OutputProducer.FlushAsync calls PipeWriter.FlushAsync
        ===>        Http2OutputProducer.FlushAsync calls IHttp2FrameWriter.FlushAsync
                        IHttp2FrameWriter is actually Http2FrameWriter
                        ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Http2/Http2FrameWriter.cs


            ======= Tracing WriteAsync path to verify that it converges with FlushAsync =======

            HttpResponseStream.WriteAsync (and Write too) calls IHttpResponseControl.WriteAsync
                IHttpResponseProtocol is actually HttpProtocol

                HttpProtocol.WriteAsync calls IHttpOutputProducer.WriteDataAsync or .WriteAsync (directly or thru WriteChunkedAsync)
                    (IHttpOutputProducer can be Http1OutputProducer or Http2OutputProducer)
                    
                    Http1OutputProducer.WriteDataAsync calls .WriteAsync on itself
                    Http1OutputProducer.WriteAsync
        --->            Http1OutputProducer.WriteAsync<T> calls .FlushAsync on itself
        --->            Http1OutputProducer other "write async" methods call a private .WriteAsync which calls .FlushAsync on itself

                    Http2OutputProducer.WriteAsync throws
                    Http2OutputProducer.WriteDataAsync calls IHttp2FrameWriter.WriteDataAsync (which is Http2FrameWriter)
                        Http2FrameWriter.WriteDataAsync calls private WriteAsync
        ===>                private WriteAsync calls .FlushAsync on itself


            =======

            I still did not fully trace it from startup to make sure all of these pieces are used.
            But I can be reasonably sure from the above.

        *)


        let setBody (stream : Stream) (response : HttpResponse) =
            // reset position to beginning
            if stream.CanSeek then
                stream.Position <- 0L

            // if stream is write-only or closed
            if not stream.CanRead then
                statusCode 501 response
                addHeader "Xiphias-Info" "\"body input stream was not readable\"" response
                Task.CompletedTask
            else             
                stream.CopyToAsync(response.Body)


        // synchronous write may throw, so don't offer the option
        // ref https://github.com/aspnet/KestrelHttpServer/blob/release/2.1/src/Kestrel.Core/Internal/Http/HttpResponseStream.cs#L66


        let writeBodyAsync (writer : Stream -> Async<unit>) (response : HttpResponse) =
            Async.StartAsTask (writer response.Body) :> Task


        let writeBodyTask (writer : Stream -> Task) (response : HttpResponse) =
            writer response.Body


        let applyMutations (httpContext : HttpContext) (response : RouteResponse) =
            let r = httpContext.Response
            // set status code
            statusCode response.StatusCode r

            // set status message
            Option.iter (fun s -> statusMessage s r) response.StatusMessage
            
            // set headers
            List.iterReverse // they are added in reverse order
                (fun (key, value) -> addHeader key value r)
                response.Headers

            // body will always return Task
            match response.Body with
                | None ->
                    Task.CompletedTask

                | Some (SetBodyStream stream) ->
                    setBody stream r

                | Some (WriteBodyStreamAsync f) ->
                    writeBodyAsync f r

                | Some (WriteBodyStreamTask f) ->
                    writeBodyTask f r


    module Internal =

        // Request was handled, but the handler did not provide a response
        let noHandlerResponse =
            Responses.response [
                Responses.statusCode 501
                Responses.header "Xiphias-Info" "\"no response from route\""
            ]


    let setResponse (context : RouteContext) =

        match context.Handler with
            | None ->
                Mutate.applyMutations context.HttpContext Internal.noHandlerResponse

            | Some (RespondNow response) ->
                Mutate.applyMutations context.HttpContext response

            | Some (RespondAfter handler) ->
                let response = handler context.HttpContext
                Mutate.applyMutations context.HttpContext response

            | Some (RespondAfterAsync handler) ->
                task {
                    let! response = Async.StartAsTask (handler context.HttpContext)
                    do! Mutate.applyMutations context.HttpContext response
                } :> Task

            | Some (RespondAfterTask handler) ->
                task {
                    let! response = handler context.HttpContext
                    do! Mutate.applyMutations context.HttpContext response
                } :> Task
