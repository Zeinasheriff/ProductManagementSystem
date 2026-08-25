using FluentValidation.TestHelper;
using ProductManagement.Application.DTOs;
using ProductManagement.Application.Validators;

namespace ProductManagement.UnitTests.Validators;

public class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _sut = new();

    public static TheoryData<string, decimal, int> InvalidRequests => new()
    {
        { "", 10m, 5 },          // missing name
        { new string('x', 151), 10m, 5 }, // name too long
        { "Valid", 0m, 5 },      // zero price
        { "Valid", -1m, 5 },     // negative price
        { "Valid", 10m, -1 },    // negative stock
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void Rejects_InvalidRequests(string name, decimal price, int stock)
    {
        var result = _sut.TestValidate(new CreateProductRequest(name, "", price, stock));
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Accepts_ValidRequest()
    {
        var result = _sut.TestValidate(new CreateProductRequest("Keyboard", "Nice keys", 49.99m, 25));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rejects_DescriptionOver1000Chars()
    {
        var result = _sut.TestValidate(new CreateProductRequest("Keyboard", new string('d', 1001), 1m, 1));
        result.ShouldHaveValidationErrorFor(p => p.Description);
    }
}

public class UpdateProductRequestValidatorTests
{
    private readonly UpdateProductRequestValidator _sut = new();

    [Fact]
    public void Accepts_ValidRequest()
    {
        var result = _sut.TestValidate(new UpdateProductRequest("Monitor", "IPS", 299m, 3, IsActive: true));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Rejects_NonPositivePrice(decimal price)
    {
        var result = _sut.TestValidate(new UpdateProductRequest("Monitor", "", price, 1, true));
        result.ShouldHaveValidationErrorFor(p => p.Price);
    }
}

public class CreateOrderRequestValidatorTests
{
    private readonly CreateOrderRequestValidator _sut = new();

    [Fact]
    public void Rejects_NullItems()
    {
        var result = _sut.TestValidate(new CreateOrderRequest(null!));
        result.ShouldHaveValidationErrorFor(r => r.Items);
    }

    [Fact]
    public void Rejects_EmptyItems()
    {
        var result = _sut.TestValidate(new CreateOrderRequest(new List<CreateOrderItemRequest>()));
        result.ShouldHaveValidationErrorFor(r => r.Items);
    }

    [Theory]
    [InlineData(0, 1)]     // invalid product id
    [InlineData(1, 0)]     // zero quantity
    [InlineData(1, -3)]    // negative quantity
    [InlineData(1, 1001)]  // over per-line cap
    public void Rejects_InvalidLineItems(int productId, int quantity)
    {
        var request = new CreateOrderRequest(new List<CreateOrderItemRequest> { new(productId, quantity) });
        var result = _sut.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Accepts_ValidOrder()
    {
        var request = new CreateOrderRequest(new List<CreateOrderItemRequest>
        {
            new(1, 2),
            new(2, 999)
        });

        var result = _sut.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}