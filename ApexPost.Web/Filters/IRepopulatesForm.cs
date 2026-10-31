namespace ApexPost.Web.Filters;

// Implemented by controllers whose forms need choices reloaded before they can be redisplayed.
public interface IRepopulatesForm
{
    Task RepopulateFormAsync(object model, CancellationToken ct);
}
