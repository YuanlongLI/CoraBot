﻿using Bot.Dialogs.Provide;
using BotTests.Setup;
using Microsoft.Bot.Schema;
using Shared;
using Shared.Models;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace BotTests.Dialogs.Provide
{
    [Collection(TestCollectionName)]
    public class ProvideDialogTests : DialogTestBase
    {
        public ProvideDialogTests(TestFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task SingleCategory()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            await CreateTestFlow(ProvideDialog.Name)
                .Test("test", StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .StartTestAsync();
        }

        /*
        [Fact]
        public async Task MultipleCategories()
        {
            // Both commented out tests need multiple categories.
            // Need to use schema that isn't in the project so
            // that it can be configured differently for tests.

            var schema = Helpers.GetSchema();
            var category = schema.Categories.Last();
            var resource = category.Resources.Last();

            await CreateTestFlow(ProvideDialog.Name)
                .Test("test", StartsWith(Phrases.Provide.GetCategory))
                .Test(category.Name, StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .StartTestAsync();
        }
        
        [Fact]
        public async Task CategoryNone()
        {
            await CreateTestFlow(ProvideDialog.Name)
                .Test("test", StartsWith(Phrases.Provide.GetCategory))
                .StartTestAsync();
        }
        */
        [Fact]
        public async Task ResourceNone()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();

            await CreateTestFlow(ProvideDialog.Name)
                .Test("test", StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Send(Phrases.None)
                .AssertNoReply()
                .StartTestAsync();
        }

        [Fact]
        public async Task QuantityNoneNoExistingResource()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            await CreateTestFlow(ProvideDialog.Name)
                .Test("test", StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .Test("0", Phrases.Provide.CompleteUpdate)
                .StartTestAsync();
        }

        [Fact]
        public async Task QuantityNone()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            await CreateTestFlow(ProvideDialog.Name)
                .Send("test")
                .StartTestAsync();

            var user = await Api.GetUser(turnContext);

            await Api.Create(new Resource
            {
                CreatedById = user.Id,
                Category = category.Name,
                Name = resource.Name
            });

            await CreateTestFlow(ProvideDialog.Name)
                .AssertReply(StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .Test("0", Phrases.Provide.CompleteDelete)
                .StartTestAsync();

            var existingResource = await Api.GetResourceForUser(user, category.Name, resource.Name);
            Assert.Null(existingResource);
        }

        [Fact]
        public async Task CreateNewResource()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            await CreateTestFlow(ProvideDialog.Name)
                .Send("test")
                .StartTestAsync();

            var user = await Api.GetUser(turnContext);

            await CreateTestFlow(ProvideDialog.Name)
                .AssertReply(StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .Test(TestHelpers.DefaultQuantity.ToString(), StartsWith(Phrases.Provide.GetIsUnopened))
                .Test(TestHelpers.DefaultIsUnopened.ToString(), Phrases.Provide.CompleteCreate(user))
                .StartTestAsync();

            var newResource = await Api.GetResourceForUser(user, category.Name, resource.Name);
            Assert.NotNull(newResource);
            Assert.Equal(TestHelpers.DefaultQuantity, newResource.Quantity);
            Assert.Equal(TestHelpers.DefaultIsUnopened, newResource.IsUnopened);
        }

        [Fact]
        public async Task UpdateExistingResource()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            await CreateTestFlow(ProvideDialog.Name)
                .Send("test")
                .StartTestAsync();

            var user = await Api.GetUser(turnContext);

            await Api.Create(new Resource
            {
                CreatedById = user.Id,
                Category = category.Name,
                Name = resource.Name
            });

            await CreateTestFlow(ProvideDialog.Name)
                .AssertReply(StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .Test(TestHelpers.DefaultQuantity.ToString(), StartsWith(Phrases.Provide.GetIsUnopened))
                .Test(TestHelpers.DefaultIsUnopened.ToString(), Phrases.Provide.CompleteUpdate)
                .StartTestAsync();

            var existingResource = await Api.GetResourceForUser(user, category.Name, resource.Name);
            Assert.NotNull(existingResource);
            Assert.Equal(TestHelpers.DefaultQuantity, existingResource.Quantity);
            Assert.Equal(TestHelpers.DefaultIsUnopened, existingResource.IsUnopened);
        }

        [Fact]
        public async Task NoMatchDistance()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            var org = schema.VerifiedOrganizations.First();

            var orgUser = new User
            {
                PhoneNumber = org.PhoneNumbers.First(),
                LocationCoordinates = TestHelpers.LocationCoordinatesNewYork
            };
            await Api.Create(orgUser);

            var orgNeed = new Need
            {
                CreatedById = orgUser.Id,
                Category = category.Name,
                Name = resource.Name,
                Quantity = TestHelpers.DefaultQuantity,
                UnopenedOnly = TestHelpers.DefaultIsUnopened,
                Instructions = TestHelpers.DefaultInstructions
            };
            await Api.Create(orgNeed);

            await CreateTestFlow(ProvideDialog.Name)
                .Send("test")
                .StartTestAsync();

            var user = await Api.GetUser(turnContext);
            user.LocationCoordinates = TestHelpers.LocationCoordinatesSeattle;
            await Api.Update(user);

            await CreateTestFlow(ProvideDialog.Name)
                .AssertReply(StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .Test(TestHelpers.DefaultQuantity.ToString(), StartsWith(Phrases.Provide.GetIsUnopened))
                .Test(TestHelpers.DefaultIsUnopened.ToString(), Phrases.Provide.CompleteCreate(user))
                .AssertReply(StartsWith(Phrases.Provide.Another))
                .StartTestAsync();
        }

        /*
        [Fact]
        public async Task NoMatchCategory()
        {
            // Needs multiple categories
            // Need to use schema that isn't in the project so
            // that it can be configured differently for tests.
        }
        */
        [Fact]
        public async Task NoMatchResource()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            var org = schema.VerifiedOrganizations.First();

            var orgUser = new User
            {
                PhoneNumber = org.PhoneNumbers.First(),
                LocationCoordinates = TestHelpers.LocationCoordinatesSeattle
            };
            await Api.Create(orgUser);

            var orgNeed = new Need
            {
                CreatedById = orgUser.Id,
                Category = category.Name,
                Name = category.Resources.Last().Name,
                Quantity = TestHelpers.DefaultQuantity,
                UnopenedOnly = TestHelpers.DefaultIsUnopened,
                Instructions = TestHelpers.DefaultInstructions
            };
            await Api.Create(orgNeed);

            await CreateTestFlow(ProvideDialog.Name)
                .Send("test")
                .StartTestAsync();

            var user = await Api.GetUser(turnContext);
            user.LocationCoordinates = TestHelpers.LocationCoordinatesSeattle;
            await Api.Update(user);

            await CreateTestFlow(ProvideDialog.Name)
                .AssertReply(StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .Test(TestHelpers.DefaultQuantity.ToString(), StartsWith(Phrases.Provide.GetIsUnopened))
                .Test(TestHelpers.DefaultIsUnopened.ToString(), Phrases.Provide.CompleteCreate(user))
                .AssertReply(StartsWith(Phrases.Provide.Another))
                .StartTestAsync();
        }

        [Fact]
        public async Task NoMatchOpened()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            var org = schema.VerifiedOrganizations.First();

            var orgUser = new User
            {
                PhoneNumber = org.PhoneNumbers.First(),
                LocationCoordinates = TestHelpers.LocationCoordinatesSeattle
            };
            await Api.Create(orgUser);

            var orgNeed = new Need
            {
                CreatedById = orgUser.Id,
                Category = category.Name,
                Name = category.Resources.Last().Name,
                Quantity = TestHelpers.DefaultQuantity,
                UnopenedOnly = TestHelpers.DefaultIsUnopened,
                Instructions = TestHelpers.DefaultInstructions
            };
            await Api.Create(orgNeed);

            await CreateTestFlow(ProvideDialog.Name)
                .Send("test")
                .StartTestAsync();

            var user = await Api.GetUser(turnContext);
            user.LocationCoordinates = TestHelpers.LocationCoordinatesSeattle;
            await Api.Update(user);

            await CreateTestFlow(ProvideDialog.Name)
                .AssertReply(StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .Test(TestHelpers.DefaultQuantity.ToString(), StartsWith(Phrases.Provide.GetIsUnopened))
                .Test("false", Phrases.Provide.CompleteCreate(user))
                .AssertReply(StartsWith(Phrases.Provide.Another))
                .StartTestAsync();
        }

        [Fact]
        public async Task MatchOpened()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            var org = schema.VerifiedOrganizations.First();

            var orgUser = new User
            {
                PhoneNumber = org.PhoneNumbers.First(),
                LocationCoordinates = TestHelpers.LocationCoordinatesSeattle
            };
            await Api.Create(orgUser);

            var orgNeed = new Need
            {
                CreatedById = orgUser.Id,
                Category = category.Name,
                Name = resource.Name,
                Quantity = TestHelpers.DefaultQuantity,
                UnopenedOnly = false,
                Instructions = TestHelpers.DefaultInstructions
            };
            await Api.Create(orgNeed);

            await CreateTestFlow(ProvideDialog.Name)
                .Send("test")
                .StartTestAsync();

            var user = await Api.GetUser(turnContext);
            user.LocationCoordinates = TestHelpers.LocationCoordinatesSeattle;
            await Api.Update(user);

            await CreateTestFlow(ProvideDialog.Name)
                .AssertReply(StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .Test(TestHelpers.DefaultQuantity.ToString(), StartsWith(Phrases.Provide.GetIsUnopened))
                .Test("false", Phrases.Provide.CompleteCreate(user))
                .AssertReply(Phrases.Match.GetMessage(org.Name, orgNeed.Name, orgNeed.Quantity, orgNeed.Instructions))
                .StartTestAsync();
        }

        [Fact]
        public async Task MultipleMatches()
        {
            var schema = Helpers.GetSchema();
            var category = schema.Categories.First();
            var resource = category.Resources.First();

            var org1 = schema.VerifiedOrganizations.First();
            var org2 = schema.VerifiedOrganizations.Last();

            var orgUser1 = new User
            {
                PhoneNumber = org1.PhoneNumbers.First(),
                LocationCoordinates = TestHelpers.LocationCoordinatesSeattle
            };
            await Api.Create(orgUser1);

            var orgUser2 = new User
            {
                PhoneNumber = org2.PhoneNumbers.First(),
                LocationCoordinates = TestHelpers.LocationCoordinatesSeattle
            };
            await Api.Create(orgUser2);

            var orgNeed1 = new Need
            {
                CreatedById = orgUser1.Id,
                Category = category.Name,
                Name = resource.Name,
                Quantity = TestHelpers.DefaultQuantity,
                UnopenedOnly = TestHelpers.DefaultIsUnopened,
                Instructions = TestHelpers.DefaultInstructions
            };
            await Api.Create(orgNeed1);

            var orgNeed2 = new Need
            {
                CreatedById = orgUser2.Id,
                Category = category.Name,
                Name = resource.Name,
                Quantity = TestHelpers.DefaultQuantity,
                UnopenedOnly = TestHelpers.DefaultIsUnopened,
                Instructions = TestHelpers.DefaultInstructions
            };
            await Api.Create(orgNeed2);

            await CreateTestFlow(ProvideDialog.Name)
                .Send("test")
                .StartTestAsync();

            var user = await Api.GetUser(turnContext);
            user.LocationCoordinates = TestHelpers.LocationCoordinatesSeattle;
            await Api.Update(user);

            await CreateTestFlow(ProvideDialog.Name)
                .AssertReply(StartsWith(Phrases.Provide.GetResource(category.Name)))
                .Test(resource.Name, StartsWith(Phrases.Provide.GetQuantity(resource.Name)))
                .Test(TestHelpers.DefaultQuantity.ToString(), StartsWith(Phrases.Provide.GetIsUnopened))
                .Test(TestHelpers.DefaultIsUnopened.ToString(), Phrases.Provide.CompleteCreate(user))
                .AssertReply(Phrases.Match.GetMessage(org1.Name, orgNeed1.Name, orgNeed1.Quantity, orgNeed1.Instructions))
                .AssertReply(StartsWith(Phrases.Match.Another(1)))
                .Test("true", Phrases.Match.GetMessage(org2.Name, orgNeed2.Name, orgNeed2.Quantity, orgNeed2.Instructions))
                .AssertReply(StartsWith(Phrases.Provide.Another))
                .StartTestAsync();
        }
    }
}
