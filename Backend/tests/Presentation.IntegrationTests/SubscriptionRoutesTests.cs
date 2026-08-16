using System.Net;
using System.Net.Http.Json;
using Application.DTos.Request;
using Application.DTos.Response;
using Domain.Enums;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class SubscriptionRoutesTests
{
    private readonly ApiFactory _factory;

    public SubscriptionRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Plans_are_public()
    {
        var client = CreateClient();
        var plans = await client.GetAsync("/api/subscriptions/plans");
        plans.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await plans.ReadJsonAsync<List<SubscriptionPlanResponse>>();
        list.Should().NotBeEmpty();

        var plan = list.First(p => p.UserType == UserType.Individual);
        var byId = await client.GetAsync($"/api/subscriptions/plans/{plan.Id}");
        byId.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_checkout_confirm_has_active_current_and_cancel()
    {
        var client = CreateClient();
        var email = $"sub_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        checkout.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();

        var confirm = await client.PostAsJsonAsync("/api/subscriptions/confirm",
            new ConfirmCheckoutRequest(session.SessionId));
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        var hasActive = await client.GetAsync("/api/subscriptions/has-active");
        hasActive.StatusCode.Should().Be(HttpStatusCode.OK);
        (await hasActive.ReadJsonAsync<HasActiveSubscriptionResponse>()).HasActiveSubscription.Should().BeTrue();

        var current = await client.GetAsync("/api/subscriptions/current");
        current.StatusCode.Should().Be(HttpStatusCode.OK);

        var cancel = await client.PostAsync("/api/subscriptions/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Company_checkout_confirm_current_has_active_and_cancel()
    {
        var client = CreateClient();
        var email = $"cosub_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var companyResp = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest($"SubCo-{Guid.NewGuid():N}"));
        companyResp.EnsureSuccessStatusCode();
        var company = await companyResp.ReadJsonAsync<CompanyResponse>();

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=1")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Company);

        var checkout = await client.PostAsJsonAsync($"/api/subscriptions/company/{company.Id}/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        checkout.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();

        var confirm = await client.PostAsJsonAsync("/api/subscriptions/confirm",
            new ConfirmCheckoutRequest(session.SessionId));
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        var hasActive = await client.GetAsync($"/api/subscriptions/company/{company.Id}/has-active");
        hasActive.StatusCode.Should().Be(HttpStatusCode.OK);

        var current = await client.GetAsync($"/api/subscriptions/company/{company.Id}/current");
        current.StatusCode.Should().Be(HttpStatusCode.OK);

        var cancel = await client.PostAsync($"/api/subscriptions/company/{company.Id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Upgrade_to_company_returns_checkout()
    {
        var client = CreateClient();
        var email = $"upg_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var companyPlan = plans.First(p => p.UserType == UserType.Company);

        var upgrade = await client.PostAsJsonAsync("/api/subscriptions/upgrade-to-company",
            new UpgradeToCompanyRequest($"UpCo-{Guid.NewGuid():N}", companyPlan.Id, BillingPeriod.Monthly,
                "http://localhost/ok", "http://localhost/cancel"));
        upgrade.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_cannot_start_user_checkout()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        checkout.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_cannot_start_company_checkout()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var companyResp = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest($"AdminCo-{Guid.NewGuid():N}"));
        companyResp.EnsureSuccessStatusCode();
        var company = await companyResp.ReadJsonAsync<CompanyResponse>();

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=1")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Company);

        var checkout = await client.PostAsJsonAsync($"/api/subscriptions/company/{company.Id}/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        checkout.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_cannot_upgrade_to_company()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=1")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Company);

        var upgrade = await client.PostAsJsonAsync("/api/subscriptions/upgrade-to-company",
            new UpgradeToCompanyRequest($"AdminUpCo-{Guid.NewGuid():N}", plan.Id, BillingPeriod.Monthly,
                "http://localhost/ok", "http://localhost/cancel"));
        upgrade.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Stripe_webhook_invalid_signature_returns_400()
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
        {
            Content = new StringContent("{}"),
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", "bad-signature");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stripe_webhook_valid_signature_completes()
    {
        var client = CreateClient();
        var email = $"wh_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
        {
            Content = new StringContent(session.SessionId),
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Subscription_updated_webhook_flips_trial_to_active()
    {
        var client = CreateClient();
        var email = $"su_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);
        await _factory.SeedSubscriptionForPlanAsync(userId, plan.Id, "sub_trial");
        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        stripeSubId.Should().NotBeNullOrEmpty();

        _factory.FakePayments.QueueSubscriptionUpdated(stripeSubId, "active");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
        {
            Content = new StringContent($"subscription-updated:{stripeSubId}"),
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await client.LoginAsync(email);
        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Status.Should().Be(Domain.Enums.SubscriptionStatus.Active);
        current.TrialEndDate.Should().BeNull();
    }

    [Fact]
    public async Task Invoice_paid_webhook_applies_pending_price_change()
    {
        var client = CreateClient();
        var email = $"ip_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);
        await _factory.SeedSubscriptionForPlanAsync(userId, plan.Id, "sub_price");
        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        stripeSubId.Should().NotBeNullOrEmpty();

        await _factory.SetNextPriceAsync(userId, 42.99m);

        _factory.FakePayments.QueueInvoicePaid(stripeSubId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
        {
            Content = new StringContent($"invoice-paid:{stripeSubId}"),
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await client.LoginAsync(email);
        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Price.Should().Be(42.99m);
        current.NextPrice.Should().BeNull();
        current.NextPriceEffectiveDate.Should().BeNull();
    }

    [Fact]
    public async Task Moderator_cannot_start_user_checkout()
    {
        var client = CreateClient();
        var email = $"mod_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureModeratorRoleAsync(email);
        await client.LoginAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        checkout.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Authorized_routes_require_auth()
    {
        var client = CreateClient();
        (await client.GetAsync("/api/subscriptions/has-active")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
