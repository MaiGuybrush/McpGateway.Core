using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using McpGateway.__Department__.__System__.Services;
using McpGateway.__Department__.__System__.Tools.__ToolClass__;

namespace McpGateway.__Department__.__System__.Tests.Tools;

public class __ToolClass__ToolTests
{
    private readonly Mock<I__ToolClass__Service> _serviceMock;
    private readonly __ToolClass__Tool _tool;

    public __ToolClass__ToolTests()
    {
        _serviceMock = new Mock<I__ToolClass__Service>();
        _tool = new __ToolClass__Tool(_serviceMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ValidInput_DelegatesToServiceAndReturnsResult()
    {
        // Arrange
        var expectedResult = new __ToolClass__ResultDto(
            QueryId: "Q123",
            Shop: "TFT1",
            Status: "SUCCESS",
            Message: "OK"
        );

        _serviceMock
            .Setup(s => s.ExecuteAsync(It.IsAny<__ToolClass__Input>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.ExecuteAsync("TFT1", "Q123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Q123", result.QueryId);
        Assert.Equal("TFT1", result.Shop);
        Assert.Equal("SUCCESS", result.Status);
        _serviceMock.Verify(s => s.ExecuteAsync(It.Is<__ToolClass__Input>(i => i.Shop == "TFT1" && i.QueryId == "Q123"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
