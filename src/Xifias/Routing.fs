namespace Xifias

(**

This module contains common routing helpers and operators for defining API routes.

*)
[<AutoOpen>]
module Routing =

    open System.Threading.Tasks
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.Primitives
    open Types
    open Responses


    type RouteHandler = RouteContext -> RouteContext option


    let andThen f x =
        Option.bind f x


    let composeWith g f x =
        f x |> andThen g


    let orElse g f x =
        match f x with
            | None ->
                g x

            | x ->
                x            


    let (>>=) x f = x |> andThen f


    let (>=>) f g = f |> composeWith g


    let (<|>) f g = f |> orElse g


    let rec oneOf (handlers : ('a -> 'b option) list) (context : 'a) =
        match handlers with
            | [] ->
                None

            | handler :: remainingHandlers ->
                match handler context with
                    | Some resultContext ->
                        Some resultContext

                    | None ->
                        oneOf remainingHandlers context                                  


    let filter (keep : HttpContext -> bool) (context : RouteContext) =
        if keep context.HttpContext then
            Some context

        else
            None


    let filterValue fromContext value =
        filter (fun context -> fromContext context = value)


    /// <summary>Match the request if it has the provided remote IP.</summary>
    /// <remarks>
    ///     NOTE: This IP address will be matched against the remote IP from the TCP connection.
    ///     When following best practices -- putting Kestrel behind a reverse proxy server like
    ///     nginx, IIS, or a cloud API gateway provider -- this will match the reverse proxy address, not the client IP.
    ///</remarks>
    let remoteIp ip =
        filterValue RequestHelp.remoteIp ip


    /// <summary>Match the request if it has the provided remote port.</summary>
    /// <remarks>
    ///     NOTE: This port will be matched against the remote port from the TCP connection.
    ///     When following best practices -- putting Kestrel behind a reverse proxy server like
    ///     nginx, IIS, or a cloud API gateway provider -- this will match the reverse proxy address, not the client IP.
    ///</remarks>
    let remotePort port =
        filterValue RequestHelp.remotePort port


    /// Match the request if it has the provided local IP.
    let localIp ip =
        filterValue RequestHelp.localIp ip


    /// Match the request if it has the provided local port.
    let localPort port =
        filterValue RequestHelp.localPort port


    /// Match the request if the client certificate matches the provided condition.
    let clientCertificateFilter certf =
        filter (RequestHelp.clientCertificate >> certf)


    /// <summary>Match the request if the name on the Host header matches the provided value.</summary>
    /// <remarks>
    /// The Host header should contain the domain name of the server and optionally a port number.
    /// NOTE: This value is may be provided by the client, rendering it insecure. Use with caution.
    /// </remarks>
    let hostName (s : string) =
        filterValue RequestHelp.hostName s


    /// <summary>Match the request if the port on the Host header matches the provided value.</summary>
    /// <remarks>
    /// The Host header should contain the domain name of the server and optionally a port number.
    /// NOTE: This value is may be provided by the client, rendering it insecure. Use with caution.
    /// </remarks>
    let hostPort (i : int option) =
        filterValue RequestHelp.hostPort i


    /// Match the request if it matches the provided condition
    let headerFilter filterf =
        // not the most efficient
        filter (RequestHelp.headerFilter (fun n v -> if filterf n v then Some () else None) >> RequestHelp.toBool)


    /// Match the request if it has the provided header name and the value matches the provided condition.
    let headerValueFilter name valuef =
        filter (RequestHelp.headerValue name >> Option.map valuef >> Option.defaultValue false)


    /// Match the request if it has the provided header name
    let headerName (name : string) =
        filter (RequestHelp.hasHeaderName name)


    /// Match the request if it has the provided header and value.
    let headerValue (name : string) (value : string) =
        headerValueFilter name (fun v -> v = StringValues(value))


    /// <summary>Match the request if the client IP matches the provided string from the provided header name.</summary>
    /// <remarks>
    /// NOTE: Headers are insecure and unreliable since they can be set from clients.
    /// This function is provided so you can check for the specific headers that you know your are set appropiately by your hosting environment.
    /// Here is an example:
    /// <code>
    /// let clientIp ipString =
    ///     clientIpFromHeader "X-Forwarded-For" ipString
    ///         <|> clientIpFromHeader "X-Real-IP" ipString
    /// </code>
    /// </remarks>
    let clientIpFromHeader (headerName : string) (ipString : string) =
        filter (
            RequestHelp.clientIpFromHeader headerName
                >> Option.map (fun ip -> Some ip = RequestHelp.parseIp ipString)
                >> Option.defaultValue false
        )


    /// Match the request if it has the provided content type.
    let contentType (typeName : string) =
        filterValue RequestHelp.contentType (Some typeName)


    /// Match the request if a cookie is present with the provided name and matches the provided condition.
    let cookieFilter filterf =
        filter (RequestHelp.cookieFilter (fun n v -> if filterf n v then Some () else None) >> RequestHelp.toBool)


    /// Match the request if it has the provided cookie name and the value matches the provided condition.
    let cookieValueFilter name valuef =
        filter (RequestHelp.cookieValue name >> Option.map valuef >> Option.defaultValue false)


    /// Match the request if a cookie is present with the provided name.
    let cookieName name =
        filter (RequestHelp.hasCookieName name)


    /// Match the request if a cookie is present with the provided name and value.
    let cookieValue name value =
        filter (RequestHelp.cookieValue name >> (fun v -> v = Some value))


    /// Match the request if it is HTTPS protocol. This is verified by the presence of a secure connection rather than the URL scheme.
    let isHttps =
        filter RequestHelp.isHttp


    /// Match the request if it uses the provided scheme. For example: in http://my.foo:123/bar, "http" is the scheme
    let scheme s =
        filterValue RequestHelp.scheme s


    /// Match the request if it uses the provided protocol. For example: HTTP/1.1
    let protocol s =
        filterValue RequestHelp.protocol s


    /// Match the request if it uses the provide HTTP method. Examples: GET, POST
    let httpVerb (verb : string) =
        filterValue RequestHelp.verb verb


    /// Match the request if it uses the GET method.
    let GET     : RouteHandler = httpVerb "GET"
    /// Match the request if it uses the POST method.
    let POST    : RouteHandler = httpVerb "POST"
    /// Match the request if it uses the PUT method.
    let PUT     : RouteHandler = httpVerb "PUT"
    /// Match the request if it uses the PATCH method.
    let PATCH   : RouteHandler = httpVerb "PATCH"
    /// Match the request if it uses the DELETE method.
    let DELETE  : RouteHandler = httpVerb "DELETE"
    /// Match the request if it uses the OPTIONS method.
    let OPTIONS : RouteHandler = httpVerb "OPTIONS"


    /// Match the request if it uses the exact provided path, case insensitive.
    let path s =
        filter (fun x -> x.Request.Path.Equals(PathString(s)))


    /// Match the request if it starts with the provided path, case insensitive.
    let pathPrefix s (context : RouteContext) =
        filter (fun x -> x.Request.Path.StartsWithSegments(PathString(s))) context


    /// Match the request if it matches the provided parser. Parsed parameters are passed into the provided function.
    let pathParse parser f (context : RouteContext) : RouteContext option =
        UrlParser.parseFromContext (UrlParser.map f parser) context.HttpContext
            |> Option.bind (fun h -> h context)


    /// Match the request if it has an authenticated user.
    /// Authentication is expected to be handled by other middleware, such as Microsoft.AspNetCore.Authentication.*.
    let authenticated =
        filter (
            fun x ->
                not (isNull x.User)
                && not (isNull x.User.Identity)
                && x.User.Identities |> Seq.exists (fun u -> u.IsAuthenticated)
        )


    /// Match the request if it has a user with the given type of claim.
    /// Populating claims is expected to be handled by other middleware, such as Microsoft.AspNetCore.Authentication.*.
    let claimType (typeName : string) =
        filter (RequestHelp.hasClaimOfType typeName)


    /// Match the request if it has a user with the given exact claim (type and value).
    /// Populating claims is expected to be handled by other middleware, such as Microsoft.AspNetCore.Authentication.*.
    let claim (typeName: string) (value : string) =
        filter (RequestHelp.hasClaim typeName value)
            

    let private ifNotResponded response (context : RouteContext) =
        match context.Handler with
            | Some (RespondNow _) ->
                Some context // ignore new response

            | _ ->
                Some { context with Handler = Some response }


    /// Set the synchronous handler which will process this request. This handler will not be executed until after all parts of the route are matched.
    let handler (func : HttpContext -> RouteResponse) (context : RouteContext) =
        ifNotResponded (RespondAfter func) context


    /// Set the Async-returning handler which will process this request. This handler will not be executed until after all parts of the route are matched.
    let handlerAsync (func : HttpContext -> Async<RouteResponse>) (context : RouteContext) =
        ifNotResponded (RespondAfterAsync func) context
    

    /// Set the Task-returning handler which will process the request. This handler will not be executed until after all parts of the route are matched.
    let handlerTask (func : HttpContext -> Task<RouteResponse>) (context : RouteContext) =
        ifNotResponded (RespondAfterTask func) context


    /// Stop matching routes and return the provided response immediately.
    let stopWith (responseItems : (RouteResponse -> RouteResponse) list) (context : RouteContext) =
        Some { context with Handler = Some (RespondNow (response responseItems)) }


    /// Match the route when there is an authenticated user present on the request.
    /// If there is no authenticated user, stop matching other routes and return a 401 immediately.
    let stopUnlessAuthenticated =
    #if DEBUG
        let toText = sprintf "%s requires authentication"
        authenticated
            <|> ( fun context ->
                    stopWith [
                        statusCode 401
                        contentText (toText (context.HttpContext.Request.Path.ToString()))
                    ] context
                )
    #else
        authenticated
            <|> stopWith [ statusCode 401 ]
    #endif


    /// Match the route when there is a user with the provided claim type present on the request.
    /// If the claim type is not present, stop matching other routes and return 403 immediately.
    let stopUnlessClaimType (typeName : string) =
    #if DEBUG
        let toText = sprintf "%s requires claim %s"
        claimType typeName
            <|> ( fun context ->
                    stopWith [
                        statusCode 403
                        contentText (toText (context.HttpContext.Request.Path.ToString()) typeName)
                    ] context
                )
    #else
        claimType typeName
            <|> stopWith [ statusCode 403 ]
    #endif


    /// Match the route when there is a user with the exact provided claim present on the request.
    /// If the exact claim is not present, stop matching other routes and return 403 immediately.
    let stopUnlessClaim (typeName : string) (value : string) =
    #if DEBUG
        let toText = sprintf "%s requires claim %s with value %s"
        claim typeName value
            <|> ( fun context ->
                    stopWith [
                        statusCode 403
                        contentText (toText (context.HttpContext.Request.Path.ToString()) typeName value)
                    ] context
                )
    #else
        claim typeName value
            <|> stopWith [ statusCode 403 ]
    #endif


    /// Matches the route when it is over a secure connection.
    /// If a secure connection is not present, stop matching other routes and respond with a permanent redirect to the same URL, but with the "https" scheme.
    let redirectIfNotHttps =
        isHttps
            <|> fun context ->
                    let r = context.HttpContext.Request
                    let location =
                        System.Text.StringBuilder()
                            .Append("https://")
                            .Append(r.Host)
                            .Append(r.PathBase)
                            .Append(r.Path)
                            .Append(r.QueryString)
                            .ToString()

                    stopWith [ redirectPermanent location ] context
    


