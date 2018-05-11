# Xifias

Functional web routing on Kestrel

> This document is an incomplete draft.

## Why?

I could not find exactly what I wanted in Suave and Giraffe. So I made Xifias to reuse in my own projects.

> Both [Suave](https://suave.io/) and [Giraffe](https://github.com/giraffe-fsharp/Giraffe) are amazing projects. They will probably suit more of your needs than this library. So check them out!

Here are some bullet point differences.

* URL parsing inspired by [evancz/url-parser](https://github.com/evancz/url-parser) from Elm  
    
  ```fsharp
  // matches path like "/api/%s/%i", provides to function as curried arguments
  pathParse (s "api" </> string </> int) (fun aString anInt -> ...)
  ```

* Synchronous routing, asynchronous handling  
  _Avoid paying a performance penalty by wrapping synchronous route decisions in `async` or `Task`._

* Use either synchronous, `async`, or `Task` handlers  
  _Choose your adventure._

* (like Giraffe) Runs on aspnet core, and inherits that middleware ecosystem  
  _Suave appears to have aspnet integration now._

* (similar to Suave) Does not mutate `HttpContext` during routing  
  _Kestrel requires HttpContext to be mutated to respond, so changes are applied after all decisions are made._

* (similar to Suave) Simple route filters -- return None if it does not match
    
  ```fsharp
  // equivalent to build-in `path` function
  let path s (context : RouteContext) : RouteContext option =
      if context.HttpContext.Request.Path.Equals(PathString(s))) then
          Some context

      else
          None

  // only respond if path is "/hello"
  POST >=> path "/hello" >=>
      stopWith [
          statusCode 200
          contentText "hi back!"
      ]
  ```



## Show me what it looks like

Here is an example API route with comments.

```fsharp
open Xifias.Routing
open Xifias.Responses
open Xifias.UrlParser

let apiRoute =
    oneOf [
        GET >=> path "/" >=>
            // respond "synchronously" (-ish. Body is always written asynchronously.)
            stopWith [
                statusCode 200
                contentText "Hello world!"
            ]

        POST >=>
            oneOf [
                // matches /message/%s
                pathParse (s "message" </> string) messageRoute
                // matches /util/newids/%i
                pathParse (s "util" </> s "newids" </> int) newIdRoute
            ]

        // matches /search, provides q, page, and size parameters as option arguments 
        GET >=> pathParse (s "search" <?> stringParam "q" <?> intParam "page" <?> intParam "size") searchRoute
        
        // default if nothing else matches
        stopWith [ statusCode 404 ]
    ]
```

Here are the `messageRoute`, `newIdRoute`, and `searchRoute` functions.

```fsharp
let messageRoute msgName =
    // require user to have claim for message, then run a `Async<>`-producing handler 
    requireClaimType msgName >=> handlerAsync (MyHandlerModule.processMessage msgName)


let newIdRoute count =
    // require user to be authenticated, then run `System.Task<>`-producing handler
    requireAuthentication >=> handlerTask (MyHandlerModule.createGuids count)


let searchRoute q page size =
    let text =
        sprintf "query %s, page %i, size %i"
            <| Option.defaultValue "" q
            <| Option.defaultValue 1 page
            <| Option.defaultValue 10 size
    // directly return a response instead of using a handler
    stopWith [
        statusCode 200
        contentText text
    ]
```



## Wiring it into Kestrel

_TODO_



## Types



### `RouteContext`

A record containing the original `HttpContext` and handler / response decisions.

This is used inside of `RouteHandler`s. See below for examples.



### `RouteHandler`

An type of function with the definition `RouteContext -> RouteContext option`.

This is where you access the `RouteContext`. The two things to do with `RouteContext` are to check the underlying `HttpContext` for filtering purposes and setting the `ResponseDecision`. Then you decide whether to return `Some context` if you want to handle the request or `None` to skip it.

Example usage

```fsharp
open Microsoft.FSharp.Linq.NullableOperators // for ?=

// only continue if the port matches
let port (i : int) (context : RouteContext) =
    if context.HttpContext.Request.Host.Port ?= i then
        Some context
        
    else
        None

// teapot response: https://en.wikipedia.org/wiki/Hyper_Text_Coffee_Pot_Control_Protocol
let teaTime (context: RouteContext) =
    let routeResponse = response [ statusCode 418; contentText "I'm a teapot" ]
    let handler = RespondNow routeResponse
    Some { context with Handler = Some handler }
```

> For `teaTime` it would have been shorter to use the `stopWith` helper like this  
> `stopWith [ statusCode 418; contentText "I'm a teapot" ]`



## Operators

Here are the operators defined by the library for routing. Their corresponding functions are also listed and demonstrated.

> _For those who are not familiar with the operators below, note that they were not chosen arbitrary. They are somewhat well-known for the kind of function they perform. Here they are used on `option` types, but you may readily observe them being used for nearly the same thing in other functional languages or on other types. For more information, check out Scott Wlaschin's [series on the "Elevated World"](https://fsharpforfunandprofit.com/posts/elevated-world/)._



### `>=>` or `composeWith`

This operator combines two RouteHandler functions into a single function.

We could combine the two example RouteHandlers above, so any requests to port 418 would get routed to a `teaTime` reponse.

```fsharp
let teaRoute =
    port 418 >=> teaTime

// alternatively without symbols

let teaRoute =
    port 418
        |> composeWith teaTime
```



### `>>=` and `andThen`

This operator, _commonly called `bind`_, is helpful for using other RouteHandlers from inside your own RouteHandlers. Whereas `>=>` is used to combine **functions** to run later, this operator is used when I already have **concrete values** to work with. We could rewrite the above to use this operator instead.

```fsharp
let teaRoute (context : RouteContext) =
    port 418 context
        >>= teaTime
        // here >=> won't work since the previous line returns a concrete value, not a function

// alternatively

let teaRoute (context : RouteContext) =
    port 418 context
        |> andThen teaTime
```

> Using `andThen` or its inline operator `>>=` is often easier to understand than the fish `>=>` operator when you write your own route handlers. So feel free to use that if it fits your brainpan better.



### `oneOf`

This function, _called `choose` in Suave and Giraffe_, takes a list of route handlers and tries each one of them in order. The first one to return a `RouteContext` will be used and none of the others will be evaluated. If all of the handlers return `None`, then this function will also return `None`.

Here is an example which takes a very specific path or else it will return a 404 Not found response.

```fsharp
let apiRoute =
    oneOf [
        path "/a/very/specific/path" >=>
            stopWith [
                statusCode 200
                contentText "You found me!"
            ]

        // this handler will always respond, if the one above does not
        stopWith [
            statusCode 404
            contentText "Nope, try again."
        ]
    ]
```



### `<|>` or `orElse`

This function, _called `tryThen` in Suave_, tries a handler then if it will not handle the request, the fallback handler is used. It is equivalent to using the `oneOf` operator with only two options. We could rewrite the oneOf example above using this operator. However, it is better used in short expressions.

Here is an example which checks for authentication and if that fails, then it responds with an error. This is almost the same as the built-in `requireAuthentication` route handler.

```fsharp
let requireAuthentication =
    authenticated <|> stopWith [ statusCode 401 ]

// alternatively

let requireAuthentication =
    authenticated
        |> orElse (stopWith [ statusCode 401 ])
```

Be careful with using `<|>` on the end of a route handler like this:

```fsharp
    oneOf [
        POST >=> path "/" >=> handler handleRoot >=> authenticated <|> stopWith [ statusCode 401 ]
        stopWith [ statusCode 404 ] // this handler will never be attempted
    ]
```

The way the handler is written, **it always responds** either using handleRoot or a 401. Nothing below it ever gets a chance. The reason is because even though the `>=>` operator will not execute the next route handler if the previous route handler returned `None`, it will keep chaining that `None` value forward. When the `None` hits `<|>`, then it will use the fallback route handler. And the fallback handler `stopWith` _always_ responds. Use of parenthesis would fix that.

```fsharp
    oneOf [
        // now part of a single function that won't be executed     vvv
        POST >=> path "/" >=> handler handleRoot >=> (authenticated <|> stopWith [ statusCode 401 ])
        stopWith [ statusCode 404 ] // will work as a default now
    ]
```

So my advice is to use parenthesis when you use `<|>` in a larger expression. When in doubt, it is usually more clear to use `oneOf`.



### `stopWith`

This function is used (only from route handlers) to immediately return a response. When you use `stopWith` any handlers you may have previously set with `handlerXXXXX` will be discarded. If you try to use a `handler` function after a `stopWith` has been issued, they will be ignored and the response will be retained.

Both of these routes will return 410 Gone.

```fsharp
let route1 =
    handler hopesWillBeDashed >=> stopWith [ statusCode 410 ]

let route2 =
    stopWith [ statusCode 410 ] >=> handler neverGonnaHappen
```

However, `stopWith` will overwrite previous uses of `stopWith`.

```fsharp
let route1 =
    stopWith [ statusCode 200 ] >=> stopWith [ statusCode 410 ]
    // returns 410 Gone to client

let route2 =
    stopWith [ statusCode 410 ] >=> stopWith [ statusCode 200 ]
    // returns 200 OK to client
```

> This is designed to prioritize responses issued during routing. However, you can define and use your own route handlers that behave differently. Use the source code for `stopWith`, `handler`, `handlerAsync`, and `handlerTask` is very short and available as a reference.



### `handler`, `handlerAsync`, and `handlerTask`

These route handlers allow you to provide a function to (eventually) process the request. The processing function you provide will not be invoked until after all routing decisions have been made. So don't worry about having to put `handlerXXXXX` helpers in a specific order. But remember that `stopXXXXX` can skip executing the processing function and return early. (`stopWith` always does.)

These two routes are equivalent. Neither of them will run the handler unless it is a POST request at the given path.

```fsharp
let myRoute =
    POST >=> path "/something" >=> handler MyLogic.something

// same as

let myRoute =
    handler MyLogic.something >=> path "/something" >=> POST
```

Here are the handler helpers and the type of functions they expect.

Helper         | Argument Type
---------------|--------------
`handler`      | `HttpContext -> RouteResponse`
`handlerAsync` | `HttpContext -> Async<RouteResponse>`
`handlerTask`  | `HttpContext -> Task<RouteResponse>`

_Technically, you can mutate the `HttpContext` directly from your handler code. But please don't do that unless you know **exactly** what you are doing. Otherwise you can create ripple-effect bugs which are hard to find. If something is missing from Xifias where you have to mutate or call methods directly on HttpContext, please file an issue and explain what you need._

> For `HttpContext`, install the nuget package `Microsoft.AspNetCore.Http.Abstractions`, and open the namespace `Microsoft.AspNetCore.Http`.  
> For `RouteResponse`, open the namespace `Xifias.Types`.



### `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, and `httpVerb`

These functions check for specific HTTP verbs. For instance, you could define an OPTIONS handler like this.

```fsharp
let OPTIONS : RouteHandler = httpVerb "OPTIONS"
```

Then you could use it in this way.

```fsharp
let myRoute =
    OPTIONS >=> path "/api/root" >=> handler rootOptions
```



### `path`

This matches an exact path, case insensitive.

```fsharp
let myRoute =
    // this will match /api/SOMETHING and /api/something, but not /api/somethingS
    GET >=> path "/api/something" >=> handler someFn
```



### `pathPrefix`

This matches the beginning of a path, case insensitive.

```fsharp
let myRoute =
    // matches /api and /aperture/science
    GET >=> pathPrefix "/ap" >=> handler someFn
```


### `pathParse`

This uses parsers to match and extra values from the request path. It passes the values as curried values to the next RouteHandler.

```fsharp
let query (name : string) (q : string option) (page : int option) (size : int option) =
    let text =
        sprintf "You asked for a list of %s matching %s, starting with page %i, with %i results per page." name
            <| Option.defaultValue "" q
            <| Option.defaultValue 1 page
            <| Option.defaultValue 10 size
    stopWith [
        statusCode 200
        contentText text
    ]

let queryRoute =
    // matches /api/query/%s with optional query parameters q, page, size
    GET >=> pathParse (s "api" </> s "query" </> string <?> stringParam "q" <?> intParam "page" <?> intParam "size") query
```

Here are the path parsing functions available.

function      | description
--------------|------------
`s`           | Matches a literal string. `s "api"` matches "/api"
`</>`         | Combines two different path parts. `s "api" </> s "query"` matches "/api/query"
`string`      | Matches a string on the path. `s "api" </> string` matches "/api/%s". The matched string is provided as an argument.
`int`         | Matches an int on the path. `s "orders" </> int` matches "/orders/%i". The matched integer is provided as an argument.
`guid`        | Matches a GUID on the path. `s "customers" </> guid` matches "/orders/%s" where %s can be converted to GUID. The matched GUID is provided as an argument
`custom`      | Allows you to provide your own parser like `string`, `int`, and `guid`. See the source code for examples.
`<?>`         | Combines a parth part and a query string part. `s "query" <?> stringParam "q"` matches "/query" and provides a string parameter `q` as an optional value.
`stringParam` | Parses a string query value. `s "query" <?> stringParam "q"` matches "/query" and provides the value of `q` as a `string option` argument.
`intParam`    | Parses an int query value. `s "query" <?> intParam "size"` matches "/query" and provides the value of `size` as an `int option` argument.
`guidParam`   | Parses a guid query value. `s "query" <?> guidParam "uuid"` matches "/query" and provides the value of `uuid` as a `guid option` argument.
`customParam` | Allows you to provide your own param parser like `stringParam`, `intParam`, and `guidParam`. See the source code for examples.



### `authenticated`

This filters out requests where the user has not authenticated. For example, if you plug in OAuth middleware before Xiphias, it will populate the HttpContext.User object and the `authenticated` handler will match. But if the user was not authenticated, the route will not be selected.

```fsharp
let routes =
    oneOf
        [
            GET >=> path "/members/only" >=> authenticated >=> handler superSecretStuff
            stopWith [ respond 404 ]
        ]
```

In the above example, users who have already authenticated and GET /members/only will be passed to the `superSecretStuff` handler. But for unauthenticated users, the API replies that it does not know what you are talking about even if you go to the correct path.



### `stopUnlessAuthenticated`

This function requires an authenticated user or else it will immediately return a 401 status code, bypassing any handlers.

```fsharp
let routes =
    oneOf [
        POST >=> path "/api/customer" >=> handler createCustomer >=> stopUnlessAuthenticated
        stopWith [ statusCode 404 ]
    ]
```

If the user has authenticated, the request will be passed on to the `createCustomer` function. Otherwise, a 401 status code is returned. Note here that the order of and `stopXXXXX` RouteHandler matters. In the above, a user will only get the 401 error if it is a POST /api/customer request but unauthenticated. If you were to put `stopUnlessAuthenticated` before the `path`, then _all_ `POST` requests will return 401 unless it matched the path /api/customer and was authenticated.

> I am sure half of you are already looking for your pitch fork and lighting your torch because 401 Unauthorized is used to represent "unauthenticated". Rather than send me a nastygram explaining why this is wrong, it will be far more effective to check out the section on Adding Your Own Route Handlers. You can also override the existing ones.



### `hasClaimType`

This filters out requests unless they have an authenticated user with a specific claim type. For example, if you plug in JWT middleware before Xiphias, that middleware will populate the HttpContext.User object along with any claims attached to the token. But if the user was not authenticated, there will be no claims.

```fsharp
let routes =
    oneOf
        [
            GET >=> path "/members/only" >=> hasClaimType "http://myapi/PlatinumStatus" >=> handler whales
            GET >=> path "/members/only" >=> authenticated >=> handler members
            stopWith [ respond 404 ]
        ]
```

In this example, users with the `PlatinumStatus` claim who GET /members/only are directed to the `whales` handler. Other authenticated users who GET /members/only are directed to the `members` handler. For everyone else, the API response with 404 Not found regardless of path or verb used.



### `stopUnlessClaimType`

This function requires an authenticated user to have a particular type of claim or it will immediately return a 403 status code, bypassing any handlers.

```fsharp
let routes =
    oneOf [
        POST >=> path "/api/customer" >=> handler createCustomer >=> stopUnlessClaimType "http://myapi/CreateCustomer"
        stopWith [ statusCode 404 ]
    ]
```

If the user has authenticated and they have the listed claim, the request will be passed on to the `createCustomer` function. Otherwise, a 403 status code is returned. As with any `stopXXXXX` handler, the order it is given matters. If you put it before `POST`, _all_ requests will return a 403 status code unless they are POST /api/customer and the user has the listed claim.

> I am sure half of you are already looking for your pitch fork and lighting your torch because 403 Forbidden is used to represent "unauthorized". Rather than send me a nastygram explaining why this is wrong, it will be far more effective to check out the section on Adding Your Own Route Handlers. You can also override the existing ones.


-- TODO change to URL versions
### `hostHeader`

Match the request's host header value (without the port).

```fsharp
let localRoute =
    GET >=> requestHost "localhost" >=> path "/localapi" >=> handler thisIsSuspect
```



### `hostHeaderPort`

Match request host header port only.

```fsharp
let localRoute =
    GET >=> requestPort 12345 >=> path "/localapi" >=> handler thisIsSuspect
```



### `response`

This function is suitable to use from your internal API code to communicate how you want to respond. This is found in the `Responses` module, along with many other response-related functions. The module provides a nice DSL so you can respond exactly the way you want, without having to perform icky mutations on the `HttpContext`. We will get more into the specific response helpers further down.

Here is an example response you might return from a particular handler.

```fsharp
let myApiCode (context : HttpContext) =

    async {
    
        ... // stuff has happened

        return
            match finalResult with
                | Ok jsonString ->
                    response [
                        statusCode 200
                        jsonContent jsonString
                    ]

                | Error (DeserializationFailed ex) ->
                    response [
                        statusCode 400
                        contentText ex.Message // known to be sanitized
                    ]

                | Error (SerializationFailed ex) ->
                    response [
                        statusCode 500
                        contentText "Serialization failed... somehow?"
                    ]

                ... // etc.

    }

// route might look like this
POST >=> path "/something" >=> handlerAsync myApiCode >=> requireAuthentication
```

TODO CORS

TODO more built-in functions documented

TODO set cookies

TODO section on replacing / extending built-in RouteHandlers with user-defined

TODO section on 501 responses, check the response headers
