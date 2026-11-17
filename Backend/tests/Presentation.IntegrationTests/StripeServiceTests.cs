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
