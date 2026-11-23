using System.Net;
using System.Text;
using FluentAssertions;
using Infrastructure.Payment;
using Microsoft.Extensions.Options;
using Stripe;

namespace Presentation.IntegrationTests;

public sealed class StripeServiceTests
{
    [Fact]
    public async Task Cancel_immediately_treats_missing_subscription_as_success()
    {
        var service = CreateService(HttpStatusCode.NotFound,
            """{"error":{"type":"invalid_request_error","code":"resource_missing","message":"No such subscription"}}""");

        var act = () => service.CancelSubscriptionImmediatelyAsync("sub_missing");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Cancel_immediately_still_throws_other_stripe_errors()
    {
        var service = CreateService(HttpStatusCode.InternalServerError,
            """{"error":{"type":"api_error","message":"Stripe is unavailable"}}""");

        var act = () => service.CancelSubscriptionImmediatelyAsync("sub_test");

        await act.Should().ThrowAsync<StripeException>();
    }

    [Fact]
    public async Task Cancel_at_period_end_reports_missing_subscription_instead_of_throwing()
    {
        var service = CreateService(HttpStatusCode.NotFound,
            """{"error":{"type":"invalid_request_error","code":"resource_missing","message":"No such subscription"}}""");

        var scheduled = await service.CancelSubscriptionAtPeriodEndAsync("sub_missing");

        scheduled.Should().BeFalse();
    }

    [Fact]
    public async Task Switch_price_ignores_missing_subscription()
    {
        var service = CreateService(HttpStatusCode.NotFound,
            """{"error":{"type":"invalid_request_error","code":"resource_missing","message":"No such subscription"}}""");

        var act = () => service.SwitchSubscriptionPriceAsync("sub_missing", "price_new");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Snapshot_returns_null_when_subscription_is_missing()
    {
        var service = CreateService(HttpStatusCode.NotFound,
            """{"error":{"type":"invalid_request_error","code":"resource_missing","message":"No such subscription"}}""");

        var snapshot = await service.GetSubscriptionSnapshotAsync("sub_missing");

        snapshot.Should().BeNull();
    }

    [Fact]
    public async Task Snapshot_reads_period_end_from_subscription_items()
    {
        var service = CreateService(HttpStatusCode.OK,
            """
            {
              "id": "sub_test",
              "object": "subscription",
              "status": "active",
              "cancel_at_period_end": true,
              "items": {
                "object": "list",
                "data": [
                  { "id": "si_1", "object": "subscription_item", "current_period_end": 1893456000 }
                ]
              }
            }
            """);

        var snapshot = await service.GetSubscriptionSnapshotAsync("sub_test");

        snapshot.Should().NotBeNull();
        snapshot!.Status.Should().Be("active");
        snapshot.CancelAtPeriodEnd.Should().BeTrue();
        snapshot.CurrentPeriodEnd.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1893456000).UtcDateTime);
    }

    private static StripeService CreateService(HttpStatusCode statusCode, string body)
    {
        var httpClient = new HttpClient(new StubHandler(statusCode, body));
        var stripeClient = new StripeClient(
            "sk_test_fake",
            httpClient: new SystemNetHttpClient(httpClient));

        return new StripeService(
            Options.Create(new StripeSettings { SecretKey = "sk_test_fake", WebhookSecret = "whsec_fake" }),
            stripeClient);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public StubHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}
