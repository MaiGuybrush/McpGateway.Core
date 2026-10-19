using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Mock Ocelot API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// GET /api/users/{id}
app.MapGet("/api/users/{id}", (int id, [FromQuery] bool includeDetails) =>
{
    var user = new UserDto
    {
        Id = id,
        Name = $"User {id}",
        Email = $"user{id}@example.com",
        Details = includeDetails ? new UserDetails
        {
            CreatedAt = DateTime.UtcNow.AddDays(-id),
            LastLogin = DateTime.UtcNow.AddHours(-id),
            Status = id % 2 == 0 ? "Active" : "Inactive"
        } : null
    };
    
    return Results.Ok(user);
})
.WithName("GetUser")
.WithOpenApi(operation =>
{
    operation.Summary = "Get user information by ID";
    operation.Description = "Retrieves detailed information about a specific user.";
    operation.Parameters[0].Description = "Unique identifier of the user";
    return operation;
});

// GET /api/report/wip
app.MapGet("/api/report/wip", ([FromQuery] string? workCenter, [FromQuery] string? productLine, [FromQuery] string? startDate, [FromQuery] string? endDate) =>
{
    // Parse date parameters
    DateTime? start = null;
    DateTime? end = null;
    
    if (DateTime.TryParse(startDate, out var startDt))
        start = startDt;
    if (DateTime.TryParse(endDate, out var endDt))
        end = endDt;

    // Simulate WIP data
    var random = new Random();
    var wipData = new List<WipItem>();
    
    // Generate mock WIP items
    for (int i = 0; i < 5; i++)
    {
        wipData.Add(new WipItem
        {
            WorkOrderId = $"WO-{random.Next(1000, 9999)}",
            ProductCode = $"PROD-{random.Next(100, 999)}",
            ProductName = $"Product {random.Next(100, 999)}",
            WorkCenter = workCenter ?? $"WC-{random.Next(1, 10)}",
            ProductLine = productLine ?? $"LINE-{random.Next(1, 5)}",
            QuantityInProcess = random.Next(10, 1000),
            QuantityCompleted = random.Next(0, 500),
            Status = random.Next(0, 100) > 20 ? "In Progress" : "On Hold",
            ScheduledCompletion = DateTime.UtcNow.AddDays(random.Next(1, 30)),
            Priority = random.Next(0, 100) > 70 ? "High" : "Normal",
            YieldRate = Math.Round(random.NextDouble() * 0.3 + 0.7, 4) // 70-100%
        });
    }
    
    // Apply filters if specified
    if (!string.IsNullOrEmpty(workCenter))
        wipData = wipData.Where(w => w.WorkCenter == workCenter).ToList();
    if (!string.IsNullOrEmpty(productLine))
        wipData = wipData.Where(w => w.ProductLine == productLine).ToList();
    
    var response = new WipReport
    {
        TotalItems = wipData.Count,
        Items = wipData,
        Summary = new WipSummary
        {
            TotalQuantityInProcess = wipData.Sum(w => w.QuantityInProcess),
            TotalQuantityCompleted = wipData.Sum(w => w.QuantityCompleted),
            AverageYieldRate = wipData.Average(w => w.YieldRate),
            WorkCenters = wipData.Select(w => w.WorkCenter).Distinct().Count(),
            ProductLines = wipData.Select(w => w.ProductLine).Distinct().Count()
        },
        GeneratedAt = DateTime.UtcNow
    };
    
    return Results.Ok(response);
})
.WithName("GetWipReport")
.WithOpenApi(operation =>
{
    operation.Summary = "Get Work In Progress report";
    operation.Description = "Retrieves manufacturing work in progress data for reporting purposes.";
    operation.Parameters[0].Description = "Filter by work center";
    operation.Parameters[1].Description = "Filter by product line";
    operation.Parameters[2].Description = "Filter by start date";
    operation.Parameters[3].Description = "Filter by end date";
    return operation;
});

// POST /api/orders
app.MapPost("/api/orders", ([FromBody] OrderRequest request) =>
{
    if (request.Items == null || request.Items.Count == 0)
    {
        return Results.BadRequest(new { error = "Order must contain at least one item" });
    }

    var orderId = Guid.NewGuid().ToString();
    var total = request.Items.Sum(item => item.Quantity * item.UnitPrice);
    
    var order = new OrderDto
    {
        OrderId = orderId,
        Status = "Created",
        Total = total,
        CreatedAt = DateTime.UtcNow,
        EstimatedDelivery = request.Priority == "Express" 
            ? DateTime.UtcNow.AddDays(1) 
            : DateTime.UtcNow.AddDays(3)
    };
    
    return Results.Ok(order);
})
.WithName("CreateOrder")
.WithOpenApi(operation =>
{
    operation.Summary = "Create a new order";
    operation.Description = "Creates a new order with items and shipping information.";
    return operation;
});

app.Run();

// DTOs
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserDetails? Details { get; set; }
}

public class UserDetails
{
    public DateTime CreatedAt { get; set; }
    public DateTime LastLogin { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class OrderRequest
{
    public int UserId { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public ShippingAddress ShippingAddress { get; set; } = new();
    public string Priority { get; set; } = "Standard"; // Standard, Express
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}

public class ShippingAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class OrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime EstimatedDelivery { get; set; }
}

public class WipReport
{
    public int TotalItems { get; set; }
    public List<WipItem> Items { get; set; } = new();
    public WipSummary Summary { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class WipItem
{
    public string WorkOrderId { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WorkCenter { get; set; } = string.Empty;
    public string ProductLine { get; set; } = string.Empty;
    public int QuantityInProcess { get; set; }
    public int QuantityCompleted { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledCompletion { get; set; }
    public string Priority { get; set; } = string.Empty;
    public double YieldRate { get; set; }
}

public class WipSummary
{
    public int TotalQuantityInProcess { get; set; }
    public int TotalQuantityCompleted { get; set; }
    public double AverageYieldRate { get; set; }
    public int WorkCenters { get; set; }
    public int ProductLines { get; set; }
}