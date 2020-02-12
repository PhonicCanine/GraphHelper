# GraphHelper
 
GraphHelper adds a `Where` extension method to `IGraphServiceUsersCollectionRequest` from the `Microsoft.Graph` Nuget library.

This method allows all the functionality of the existing `Filter` method, but rather than taking a string, it takes a `Predicate<User>`, and produces the appropriate string, and applies filter automatically.

## Example

For example, to get users whose given names begins with a set filter:

```cs
var filter = "Mary";
var users = await graph.Users.Request().Filter($"startswith(givenName,{filter.Replace("'","''")})").GetAsync();
```

becomes

```cs
var filter = "Mary";
var users = await graph.Users.Request().Where((u) => u.GivenName.StartsWith(filter)).GetAsync();
```

## Supported operations
* `&` + `&&` (see also below)
* `|` + `||` (see also below)
* `(` + `)`
* `!`
* `<` + `>` + `<=` + `>=` + `==` + `!=`
* `*.StartsWith(string)`

## Important things to note

* The only method that may be called on a member of the `User` object passed in to the predicate is `.StartsWith()`. This is because the Graph API only supports `startswith()`.
* As Where is only called once, rather than with each item as would occur with a regular LINQ expression, updating values within the predicate is not recommended, and for the most part, not supported.
* Most code within the predicate does not run, and instead is merely comprehended to form a string to place into the default `Filter(string)` method from the Graph Library. Method calls on captured variables can run, however, meaning the following is valid:
    ```cs
    var filter = "Mary";
    var users = await graph.Users.Request().Where((u) => u.GivenName.StartsWith(filter.ToLower()));
    ```
    and will reduce to:
    `Filter("(startswith(givenName,'mary'))")`
* As the Graph API does not distinguish between `Or` (`|`) and `OrElse` (`||`) or `And` (`&`) and `AndAlso` (`&&`), both instances of each will reduce to `and` or `or` respectively.
* If an illegal method is executed on the predicate's parameter, a runtime exception will be thrown (`MethodNotSupportedException`)
* If an unsupported operation is used within the predicate (such as `++`), an exception will be thrown (`UnsupportedOperationException`)