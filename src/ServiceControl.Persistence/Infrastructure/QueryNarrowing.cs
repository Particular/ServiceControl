namespace ServiceControl.Persistence.Infrastructure
{
    /// <summary>
    /// The page, ordering and filters a read was narrowed by, expressed as version terms.
    /// <para>
    /// A version over a list normally tells two queries apart by the rows it returns. A query that matches
    /// nothing returns no rows and so contributes no terms, which leaves every empty view of the same data
    /// sharing one version.
    /// </para>
    /// </summary>
    public static class QueryNarrowing
    {
        public static (string Name, object? Value)[] Terms(PagingInfo pagingInfo, SortInfo? sortInfo, params (string Name, object? Value)[] filters) =>
        [
            ("page", pagingInfo.Page),
            ("pageSize", pagingInfo.PageSize),
            ("sort", sortInfo?.Sort),
            ("direction", sortInfo?.Direction),
            .. filters
        ];
    }
}
