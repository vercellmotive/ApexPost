using ApexPost.Domain.Entities;

namespace ApexPost.Web.Models;

public sealed record ProductStockViewModel(Product Product, int QuantityOnHand);
