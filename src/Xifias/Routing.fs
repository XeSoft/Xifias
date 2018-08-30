namespace Xifias

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
        filterValue Request.remoteIp ip


    /// <summary>Match the request if it has the provided remote port.</summary>
    /// <remarks>
    ///     NOTE: This port will be matched against the remote port from the TCP connection.
    ///     When following best practices -- putting Kestrel behind a reverse proxy server like
    ///     nginx, IIS, or a cloud API gateway provider -- this will match the reverse proxy address, not the client IP.
    ///</remarks>
    let remotePort port =
        filterValue Request.remotePort port


    /// Match the request if it has the provided local IP.
    let localIp ip =
        filterValue Request.localIp ip


    /// Match the request if it has the provided local port.
    let localPort port =
        filterValue Request.localPort port


    /// Match the request if the client certificate matches the provided condition.
    let clientCertificateFilter certf =
        filter (Request.clientCertificate >> certf)


    /// <summary>Match the request if the name on the Host header matches the provided value.</summary>
    /// <remarks>
    /// The Host header should contain the domain name of the server and optionally a port number.
    /// NOTE: This value is may be provided by the client, rendering it insecure. Use with caution.
    /// </remarks>
    let hostName (s : string) =
        filterValue Request.hostName s


    /// <summary>Match the request if the port on the Host header matches the provided value.</summary>
    /// <remarks>
    /// The Host header should contain the domain name of the server and optionally a port number.
    /// NOTE: This value is may be provided by the client, rendering it insecure. Use with caution.
    /// </remarks>
    let hostPort (i : int option) =
        filterValue Request.hostPort i


    /// Match the request if it matches the provided condition
    let headerFilter filterf =
        // not the most efficient
        filter (Request.headerFilter (fun n v -> if filterf n v then Some () else None) >> Request.toBool)


    /// Match the request if it has the provided header name and the value matches the provided condition.
    let headerValueFilter name valuef =
        filter (Request.headerValue name >> Option.map valuef >> Option.defaultValue false)


    /// Match the request if it has the provided header name
    let headerName (name : string) =
        filter (Request.hasHeaderName name)


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
            Request.clientIpFromHeader headerName
                >> Option.map (fun ip -> Some ip = Request.parseIp ipString)
                >> Option.defaultValue false
        )


    /// Match the request if it has the provided content type.
    let contentType (typeName : string) =
        filterValue Request.contentType (Some typeName)


    /// Match the request if a cookie is present with the provided name and matches the provided condition.
    let cookieFilter filterf =
        filter (Request.cookieFilter (fun n v -> if filterf n v then Some () else None) >> Request.toBool)


    /// Match the request if it has the provided cookie name and the value matches the provided condition.
    let cookieValueFilter name valuef =
        filter (Request.cookieValue name >> Option.map valuef >> Option.defaultValue false)


    /// Match the request if a cookie is present with the provided name.
    let cookieName name =
        filter (Request.hasCookieName name)


    /// Match the request if a cookie is present with the provided name and value.
    let cookieValue name value =
        filter (Request.cookieValue name >> (fun v -> v = Some value))


    /// Match the request if it is using the HTTPS protocol.
    let isHttps =
        filter Request.isHttp


    /// Match the request if it uses the provided scheme. For example: in http://my.foo:123/bar, "http" is the scheme
    let scheme s =
        filterValue Request.scheme s


    /// Match the request if it uses the provided protocol. For example: HTTP/1.1
    let protocol s =
        filterValue Request.protocol s


    /// Match the request if it uses the provide HTTP method. Examples: GET, POST
    let httpVerb (verb : string) =
        filterValue Request.verb verb


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
        filter (Request.hasClaimOfType typeName)


    /// Match the request if it has a user with the given exact claim (type and value).
    /// Populating claims is expected to be handled by other middleware, such as Microsoft.AspNetCore.Authentication.*.
    let claim (typeName: string) (value : string) =
        filter (Request.hasClaim typeName value)
            

    let private ifNotResponded response (context : RouteContext) =
        match context.Handler with
            | Some (RespondNow _) ->
                Some context // ignore new response

            | _ ->
                Some { context with Handler = Some response }


    /// Set the synchronous handler which will process this request. This handler will not be executed until after all parts of the route are matched.
    let handler (func : HttpContext -> Response) (context : RouteContext) =
        ifNotResponded (RespondAfter func) context


    /// Set the Async-returning handler which will process this request. This handler will not be executed until after all parts of the route are matched.
    let handlerAsync (func : HttpContext -> Async<Response>) (context : RouteContext) =
        ifNotResponded (RespondAfterAsync func) context
    

    /// Set the Task-returning handler which will process the request. This handler will not be executed until after all parts of the route are matched.
    let handlerTask (func : HttpContext -> Task<Response>) (context : RouteContext) =
        ifNotResponded (RespondAfterTask func) context


    /// Return the provided response.
    let respond (response : Response) (context : RouteContext) =
        ifNotResponded (RespondAfter (fun _ -> response)) context


    /// Return the provided response description.
    let respondWith (responseItems : (Response -> Response) list) (context : RouteContext) =
        ifNotResponded (RespondAfter (fun _ -> response responseItems)) context


    /// Stop matching routes and return the provided response immediately.
    let stop (response : Response) (context : RouteContext) =
        ifNotResponded (RespondNow response) context


    /// Stop matching routes and return the provided response description immediately.
    let stopWith (responseItems : (Response -> Response) list) (context : RouteContext) =
        ifNotResponded (RespondNow (response responseItems)) context


    /// Match the route when there is an authenticated user present on the request.
    /// If there is no authenticated user, stop matching other routes and return a 401 immediately.
    let stopUnlessAuthenticated =
        authenticated
            <|> stopWith [ statusCodeInt 401 ]


    /// Match the route when there is a user with the provided claim type present on the request.
    /// If the claim type is not present, stop matching other routes and return 403 immediately.
    let stopUnlessClaimType (typeName : string) =
        claimType typeName
            <|> stopWith [ statusCodeInt 403 ]


    /// Match the route when there is a user with the exact provided claim present on the request.
    /// If the exact claim is not present, stop matching other routes and return 403 immediately.
    let stopUnlessClaim (typeName : string) (value : string) =
        claim typeName value
            <|> stopWith [ statusCodeInt 403 ]


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
