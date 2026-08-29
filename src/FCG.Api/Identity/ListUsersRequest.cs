using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Api.Identity;

public sealed record ListUsersRequest
{
    public const int MinPage = 1;

    // (page - 1) * pageSize é calculado em int no Skip. Sem teto, page perto de int.MaxValue
    // estoura para offset negativo e o PostgreSQL rejeita com 500.
    public const int MaxPage = 1_000_000;

    public const int MinPageSize = 1;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    [FromQuery(Name = "page")]
    [Range(MinPage, MaxPage)]
    public int Page { get; init; } = MinPage;

    [FromQuery(Name = "pageSize")]
    [Range(MinPageSize, MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;

    [FromQuery(Name = "search")]
    [StringLength(256)]
    public string? Search { get; init; }
}
