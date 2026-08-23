# Data versioning design

## What it is

A **data version** is the short opaque label a query result carries so that a client asking for it again can be told "nothing has changed" instead of being sent the whole answer. On the wire it is an HTTP entity-tag: the response carries `ETag`, the client sends it back as `If-None-Match`, and a matching request is answered `304 Not Modified` with no body.

One value type carries it end to end: `DataVersion` in `src/ServiceControl.Persistence/Infrastructure/DataVersion.cs`. Every persister produces one, `QueryStatsInfo.Version` carries it out of the persistence layer, and the Web API turns it into the header. It is a `readonly struct`, so `default` is a legitimate value and no variable of the type can be null.

This is the primary (error) instance only. The audit instance still carries a `string ETag` on its own `QueryStatsInfo` and has not been converted.

## The one rule

**If a field the response renders can change without the version changing, a client caches that page for ever and nothing reveals it.** No log line, no exception, no failing test.

The promise is scoped to **one URL**, because a client only ever sends a validator back to the URL that issued it. So what must never happen is one URL answering `304` when its own body would have differed. Two different URLs sharing a value is harmless: an HTTP cache is keyed on the whole URL.

That scoping is what makes a backend's own token usable. RavenDB's result etag stands for the state of the index behind the query, so it moves on any write the query could see, but it says nothing about which page was asked for: every `/api/errors` URL shares one value, whatever the page, sort or filter. The EF Core persisters compose over the rows they returned, so theirs differ per page. Both satisfy the rule.

## Making one

| Factory                                 | Use it for                                                                                      |
| --------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `FromToken(string)` / `FromToken(long)` | a token the backend already produces, such as a RavenDB index etag or document change vector    |
| `Compose(terms)`                        | named terms over aggregates, where an aggregate provably moves with the fields it stands in for |
| `OverRows(summary, rows, fields)`       | a list the response renders row by row: summary terms for the whole set, plus one term per row  |
| `OverRows(summary, rows)`               | the same, for rows that declare their own fields by implementing `IVersionedRow`                |
| `Combine(instances)`                    | one version for a result gathered from several instances                                        |
| `FromClient(header)`                    | a validator a caller sent back, in any shape an old or current instance might emit              |

`Compose` digests its terms and emits the result as a GUID, so the tag reveals nothing about the values behind it. The terms go into the hash one at a time rather than being joined into a string first, so the largest thing ever held in memory is a single row rather than the whole page.

Every term's value, and every field inside a row, is **length prefixed**. Without that, free user text carrying a delimiter could make two different results digest identically: a failure group titled `x.y` with an empty `Type` would collide with one titled `x` whose `Type` is `y`. Term names are not prefixed, because they are literals in the code rather than anything a user can type. `Format` accepts strings, `bool`, `DateTime` and `DateTimeOffset` (both by ticks) and anything `IFormattable` under the invariant culture, and **throws** on anything else, because a type whose `ToString` is not a documented function of its content would pin the version silently.

`OverRows` names rows by position, so a caller whose query has no `ORDER BY` has to sort them first or the validator churns.

## Absence

`DataVersion.None` is `default`, and it means there is no version to offer. Two parties that both know nothing have not established that nothing changed, so **absence must never answer `304`**: an empty validator that matched itself would serve a cached page for every request for ever.

`Combine` returns `None` as soon as any instance reports none, rather than quietly claiming to cover an instance it could not see.

## Reaching the client

`WithEtag` emits **every** tag weak, as `W/"…"`. Nothing here can promise the response bytes: response compression rewrites them without touching the tag, and no endpoint enables range processing, which is the one thing an exact validator would buy. RFC 9110 requires `If-None-Match` to use the weak comparison anyway, so the marking costs nothing.

`NotModifiedStatusHttpHandler` turns a matching request into a `304`. It compares with `EntityTagHeaderValue.Compare(useStrongComparison: false)`, because `Equals` on that type compares strength as well as the tag and its own documentation says not to use it for this. `*` matches whenever a representation exists.

## Across instances

Scatter-gather endpoints merge one version per instance through `Combine`. It is keyed on instance id and sorted ordinally, so the composite is independent of the order instances answered in but still moves if two instances swap which validator they report.

An API whose own instance holds none of the data drops its own empty result before aggregating, via `AggregateStatsFromRemotesOnly`. Left in, its version-less placeholder would take the whole composite to `None` and the endpoint would emit no tag at all.
