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
    public async Task Upgrade_cancels_individual_sub_with_proration_credit()
    {
        var client = CreateClient();
        var email = $"uppr_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var individualPlan = plans.First(p => p.UserType == UserType.Individual);
        var companyPlan = plans.First(p => p.UserType == UserType.Company);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(individualPlan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Trial ends -> active (a paid individual subscription)
        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        _factory.FakePayments.QueueInvoicePaid(stripeSubId!);
        using (var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"invoice-paid:{stripeSubId}"),
               })
        {
            invoiceRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(invoiceRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var upgrade = await client.PostAsJsonAsync("/api/subscriptions/upgrade-to-company",
            new UpgradeToCompanyRequest($"UpCo-{Guid.NewGuid():N}", companyPlan.Id, BillingPeriod.Monthly,
                "http://localhost/ok", "http://localhost/cancel"));
        upgrade.StatusCode.Should().Be(HttpStatusCode.OK);

        // The individual sub is canceled with a proration credit (no throwaway), flag cleared
        var (status, cancelAtPeriodEnd) = await _factory.GetUserSubscriptionStateAsync(userId);
        status.Should().Be(Domain.Enums.SubscriptionStatus.Canceled);
        cancelAtPeriodEnd.Should().BeFalse();
        _factory.FakePayments.ProratedCancels.Should().Contain(stripeSubId);
    }

    [Fact]
    public async Task Used_individual_trial_means_no_new_company_trial()
    {
        var client = CreateClient();
        var email = $"notrial_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var individualPlan = plans.First(p => p.UserType == UserType.Individual);
        var companyPlan = plans.First(p => p.UserType == UserType.Company);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(individualPlan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Consume the individual trial (invoice paid -> fingerprint nulled)
        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        _factory.FakePayments.QueueInvoicePaid(stripeSubId!);
        using (var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"invoice-paid:{stripeSubId}"),
               })
        {
            invoiceRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(invoiceRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var trialDaysBefore = _factory.FakePayments.RecordedTrialDays.Count;
        var upgrade = await client.PostAsJsonAsync("/api/subscriptions/upgrade-to-company",
            new UpgradeToCompanyRequest($"UpCo-{Guid.NewGuid():N}", companyPlan.Id, BillingPeriod.Monthly,
                "http://localhost/ok", "http://localhost/cancel"));
        upgrade.StatusCode.Should().Be(HttpStatusCode.OK);

        // The company checkout offers no trial — the individual one was already used
        _factory.FakePayments.RecordedTrialDays.Should().HaveCount(trialDaysBefore + 1);
        _factory.FakePayments.RecordedTrialDays.Last().Should().Be(0);

        var companySession = await upgrade.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(companySession.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
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
    public async Task Checkout_with_trial_off_creates_active_subscription()
    {
        var adminClient = CreateClient();
        var adminEmail = $"admin_{Guid.NewGuid():N}@example.com";
        await adminClient.RegisterAndLoginAsync(adminEmail);
        await _factory.EnsureSoleAdminRoleAsync(adminEmail);
        await adminClient.LoginAsync(adminEmail);

        var create = await adminClient.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"Plan-{Guid.NewGuid():N}", "No trial plan", UserType.Individual, 9.99m, 99.99m, null, null, null, 0));
        create.EnsureSuccessStatusCode();
        var plan = await create.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var client = CreateClient();
        var email = $"nt_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        checkout.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();

        var confirm = await client.PostAsJsonAsync("/api/subscriptions/confirm",
            new ConfirmCheckoutRequest(session.SessionId));
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Status.Should().Be(Domain.Enums.SubscriptionStatus.Active);
        current.TrialEndDate.Should().BeNull();
        current.EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Checkout_with_trial_days_creates_trial_subscription()
    {
        var adminClient = CreateClient();
        var adminEmail = $"admin_{Guid.NewGuid():N}@example.com";
        await adminClient.RegisterAndLoginAsync(adminEmail);
        await _factory.EnsureSoleAdminRoleAsync(adminEmail);
        await adminClient.LoginAsync(adminEmail);

        var create = await adminClient.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"Plan-{Guid.NewGuid():N}", "Trial plan", UserType.Individual, 9.99m, 99.99m, null, null, null, 10));
        create.EnsureSuccessStatusCode();
        var plan = await create.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var client = CreateClient();
        var email = $"tr_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        checkout.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();

        var confirm = await client.PostAsJsonAsync("/api/subscriptions/confirm",
            new ConfirmCheckoutRequest(session.SessionId));
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Status.Should().Be(Domain.Enums.SubscriptionStatus.Trial);
        current.TrialEndDate.Should().NotBeNull();
        (current.TrialEndDate!.Value - DateTime.UtcNow).TotalDays.Should().BeInRange(9, 11);
    }

    [Fact]
    public async Task Resubscribe_after_cancel_does_not_create_duplicate_subscription()
    {
        var client = CreateClient();
        var email = $"dup_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        async Task<(string SessionId, string StripeSubId)> CheckoutAndConfirmAsync()
        {
            var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
                new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
            checkout.StatusCode.Should().Be(HttpStatusCode.OK);
            var session = await checkout.ReadJsonAsync<CheckoutResponse>();

            var confirm = await client.PostAsJsonAsync("/api/subscriptions/confirm",
                new ConfirmCheckoutRequest(session.SessionId));
            confirm.StatusCode.Should().Be(HttpStatusCode.OK);

            var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
            stripeSubId.Should().NotBeNullOrEmpty();
            return (session.SessionId, stripeSubId!);
        }

        // First subscription: trial -> active (invoice paid) -> cancel-at-period-end
        await CheckoutAndConfirmAsync();
        (await _factory.CountUserSubscriptionsAsync(userId)).Should().Be(1);

        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        _factory.FakePayments.QueueInvoicePaid(stripeSubId!);
        using (var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"invoice-paid:{stripeSubId}"),
               })
        {
            invoiceRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(invoiceRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        (await _factory.CountUserSubscriptionsAsync(userId)).Should().Be(1);

        (await client.PostAsync("/api/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _factory.CountUserSubscriptionsAsync(userId)).Should().Be(1);

        // Cancel keeps access until period end: still Active, but checkout is blocked
        var canceled = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        canceled.Status.Should().Be(Domain.Enums.SubscriptionStatus.Active);
        canceled.CancelAtPeriodEnd.Should().BeTrue();

        var blockedResubscribe = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        blockedResubscribe.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Period ends -> Stripe deletes the subscription -> local row flips to Canceled
        _factory.FakePayments.QueueSubscriptionDeleted(stripeSubId!);
        using (var deletedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"subscription-deleted:{stripeSubId}"),
               })
        {
            deletedRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(deletedRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Resubscribe after period end: still exactly one row, now active again
        await CheckoutAndConfirmAsync();
        (await _factory.CountUserSubscriptionsAsync(userId)).Should().Be(1);

        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Status.Should().Be(Domain.Enums.SubscriptionStatus.Active);
        current.TrialEndDate.Should().BeNull();
    }

    [Fact]
    public async Task Resubscribe_after_consumed_trial_gets_no_trial()
    {
        var client = CreateClient();
        var email = $"consumed_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Consume the trial by paying the first invoice (also nulls the fingerprint)
        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        _factory.FakePayments.QueueInvoicePaid(stripeSubId!);
        using (var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"invoice-paid:{stripeSubId}"),
               })
        {
            invoiceRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(invoiceRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await client.PostAsync("/api/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Cancel keeps access until period end: still Active -> checkout blocked
        var blockedResubscribe = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        blockedResubscribe.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Period ends -> Stripe deletes the subscription -> local row flips to Canceled
        _factory.FakePayments.QueueSubscriptionDeleted(stripeSubId!);
        using (var deletedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"subscription-deleted:{stripeSubId}"),
               })
        {
            deletedRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(deletedRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var trialDaysBefore = _factory.FakePayments.RecordedTrialDays.Count;
        var resubscribe = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        resubscribe.StatusCode.Should().Be(HttpStatusCode.OK);

        // The consumed trial must not be offered again — no "1 day trial" in checkout
        _factory.FakePayments.RecordedTrialDays.Should().HaveCount(trialDaysBefore + 1);
        _factory.FakePayments.RecordedTrialDays.Last().Should().Be(0);

        var session2 = await resubscribe.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session2.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Status.Should().Be(Domain.Enums.SubscriptionStatus.Active);
        current.TrialEndDate.Should().BeNull();
    }

    [Fact]
    public async Task Cancel_keeps_access_until_period_end()
    {
        var client = CreateClient();
        var email = $"keep_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Trial ends -> active (so the cancel path is the paid period-end branch)
        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        _factory.FakePayments.QueueInvoicePaid(stripeSubId!);
        using (var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"invoice-paid:{stripeSubId}"),
               })
        {
            invoiceRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(invoiceRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await client.PostAsync("/api/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Access is kept: status stays Active, has-active stays true, flag is set
        var hasActive = await (await client.GetAsync("/api/subscriptions/has-active")).ReadJsonAsync<HasActiveSubscriptionResponse>();
        hasActive.HasActiveSubscription.Should().BeTrue();

        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Status.Should().Be(Domain.Enums.SubscriptionStatus.Active);
        current.CancelAtPeriodEnd.Should().BeTrue();
        current.EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Reactivate_clears_cancel_without_new_checkout()
    {
        var client = CreateClient();
        var email = $"react_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Trial ends -> active (so the cancel path is the paid period-end branch)
        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        _factory.FakePayments.QueueInvoicePaid(stripeSubId!);
        using (var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"invoice-paid:{stripeSubId}"),
               })
        {
            invoiceRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(invoiceRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await client.PostAsync("/api/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var checkoutCountBefore = _factory.FakePayments.RecordedTrialDays.Count;

        var reactivate = await client.PostAsync("/api/subscriptions/reactivate", null);
        reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // No new checkout session was created — reactivating never charges again
        _factory.FakePayments.RecordedTrialDays.Should().HaveCount(checkoutCountBefore);

        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Status.Should().Be(Domain.Enums.SubscriptionStatus.Active);
        current.CancelAtPeriodEnd.Should().BeFalse();
        current.EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Reactivate_when_not_scheduled_to_cancel_fails()
    {
        var client = CreateClient();
        var email = $"reactbad_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var reactivate = await client.PostAsync("/api/subscriptions/reactivate", null);
        reactivate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reactivate_after_subscription_already_ended_returns_bad_request()
    {
        var client = CreateClient();
        var email = $"reactend_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Trial ends -> active, then cancel-at-period-end
        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        _factory.FakePayments.QueueInvoicePaid(stripeSubId!);
        using (var invoiceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"invoice-paid:{stripeSubId}"),
               })
        {
            invoiceRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(invoiceRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        (await client.PostAsync("/api/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Simulate the race: period already ended in Stripe, deleted webhook not yet processed
        _factory.FakePayments.ReactivateFailures.Add(stripeSubId!);

        var reactivate = await client.PostAsync("/api/subscriptions/reactivate", null);
        reactivate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Active_subscription_with_passed_end_date_is_not_has_active()
    {
        var client = CreateClient();
        var email = $"expired_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Simulate a missed customer.subscription.deleted webhook: row still Active but period ended
        await _factory.SetUserSubscriptionActiveAsync(userId, cancelAtPeriodEnd: true);
        await _factory.SetUserSubscriptionEndDateAsync(userId, DateTime.UtcNow.AddDays(-1));

        var hasActive = await (await client.GetAsync("/api/subscriptions/has-active")).ReadJsonAsync<HasActiveSubscriptionResponse>();
        hasActive.HasActiveSubscription.Should().BeFalse();
    }

    [Fact]
    public async Task Resubscribe_during_trial_resumes_remaining_days()
    {
        var client = CreateClient();
        var email = $"resume_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);

        // Cancel mid-trial: the trial keeps running (auto-renewal disabled), so the
        // subscription still counts as active and checkout stays blocked until it ends.
        (await client.PostAsync("/api/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var blocked = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Trial ends -> Stripe deletes the subscription -> local row flips to Canceled
        _factory.FakePayments.QueueSubscriptionDeleted(stripeSubId!);
        using (var deletedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"subscription-deleted:{stripeSubId}"),
               })
        {
            deletedRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(deletedRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Fingerprint still has a future TrialEndDate -> remaining days resume
        var trialDaysBefore = _factory.FakePayments.RecordedTrialDays.Count;
        var resubscribe = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        resubscribe.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.FakePayments.RecordedTrialDays.Should().HaveCount(trialDaysBefore + 1);
        _factory.FakePayments.RecordedTrialDays.Last().Should().BeGreaterThan(0);

        var session2 = await resubscribe.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session2.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Status.Should().Be(Domain.Enums.SubscriptionStatus.Trial);
        current.TrialEndDate.Should().NotBeNull();
        (current.TrialEndDate!.Value - DateTime.UtcNow).TotalDays.Should().BeInRange(1, 8);
    }

    [Fact]
    public async Task Cancel_trial_keeps_access_and_disables_auto_renewal()
    {
        var client = CreateClient();
        var email = $"trialcancel_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var plans = await (await client.GetAsync("/api/subscriptions/plans?userType=0")).ReadJsonAsync<List<SubscriptionPlanResponse>>();
        var plan = plans.First(p => p.UserType == UserType.Individual);

        var checkout = await client.PostAsJsonAsync("/api/subscriptions/create-checkout",
            new SubscribeRequest(plan.Id, BillingPeriod.Monthly, "http://localhost/ok", "http://localhost/cancel"));
        var session = await checkout.ReadJsonAsync<CheckoutResponse>();
        (await client.PostAsJsonAsync("/api/subscriptions/confirm", new ConfirmCheckoutRequest(session.SessionId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var before = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);

        // Cancel: trial keeps running, only auto-renewal is disabled
        (await client.PostAsync("/api/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var hasActive = await (await client.GetAsync("/api/subscriptions/has-active")).ReadJsonAsync<HasActiveSubscriptionResponse>();
        hasActive.HasActiveSubscription.Should().BeTrue();

        var after = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        after.Status.Should().Be(Domain.Enums.SubscriptionStatus.Trial);
        after.CancelAtPeriodEnd.Should().BeTrue();
        after.TrialEndDate.Should().Be(before.TrialEndDate);

        // Reactivate re-enables auto-renewal without a new checkout
        var checkoutCountBefore = _factory.FakePayments.RecordedTrialDays.Count;
        (await client.PostAsync("/api/subscriptions/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.FakePayments.RecordedTrialDays.Should().HaveCount(checkoutCountBefore);

        var reactivated = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        reactivated.Status.Should().Be(Domain.Enums.SubscriptionStatus.Trial);
        reactivated.CancelAtPeriodEnd.Should().BeFalse();
        reactivated.TrialEndDate.Should().Be(before.TrialEndDate);

        // Trial ends -> Stripe deletes the subscription -> local row flips to Canceled
        _factory.FakePayments.QueueSubscriptionDeleted(stripeSubId!);
        using (var deletedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/subscriptions/stripe-webhook")
               {
                   Content = new StringContent($"subscription-deleted:{stripeSubId}"),
               })
        {
            deletedRequest.Headers.TryAddWithoutValidation("Stripe-Signature", "valid-test-signature");
            (await client.SendAsync(deletedRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var hasActiveAfterEnd = await (await client.GetAsync("/api/subscriptions/has-active")).ReadJsonAsync<HasActiveSubscriptionResponse>();
        hasActiveAfterEnd.HasActiveSubscription.Should().BeFalse();
    }

    [Fact]
    public async Task Authorized_routes_require_auth()
    {
        var client = CreateClient();
        (await client.GetAsync("/api/subscriptions/has-active")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
