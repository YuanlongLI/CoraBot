﻿using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Spatial;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Extensions.Configuration;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.ApiInterface
{
    /// <summary>
    /// API interface for Cosmos
    /// </summary>
    public class CosmosInterface : IApiInterface
    {
        private IConfiguration config;
        private DocumentClient client;

        public CosmosInterface(IConfiguration config)
        {
            this.config = config;
            this.client = new DocumentClient(new Uri(config.CosmosEndpoint()), config.CosmosKey());
            this.client.OpenAsync();
        }

        /// <summary>
        /// Creates a new record.
        /// </summary>
        public async Task<string> Create(Model model)
        {
            var collection = GetCollection(model);
            if (string.IsNullOrEmpty(collection))
            {
                return string.Empty;
            }

            var collectionUri = UriFactory.CreateDocumentCollectionUri(this.config.CosmosDatabase(), collection);
            await client.CreateDocumentAsync(collectionUri, model);
            return model.Id;
        }

        /// <summary>
        /// Deletes a record.
        /// </summary>
        public async Task<bool> Delete(Model model)
        {
            var collection = GetCollection(model);
            if (string.IsNullOrEmpty(collection))
            {
                return false;
            }

            var documentUri = UriFactory.CreateDocumentUri(this.config.CosmosDatabase(), collection, model.Id);
            await client.DeleteDocumentAsync(documentUri);
            return true;
        }

        /// <summary>
        /// Saves changes to a record.
        /// </summary>
        public async Task<bool> Update(Model model)
        {
            var collection = GetCollection(model);
            if (string.IsNullOrEmpty(collection))
            {
                return false;
            }

            var documentUri = UriFactory.CreateDocumentUri(this.config.CosmosDatabase(), collection, model.Id);
            await client.ReplaceDocumentAsync(documentUri, model);
            return true;
        }

        /// <summary>
        /// Gets a user from a turn context.
        /// </summary>
        public async Task<User> GetUser(ITurnContext turnContext)
        {
            var userToken = Helpers.GetUserToken(turnContext);

            switch (turnContext.Activity.ChannelId)
            {
                case Channels.Emulator:
                case Channels.Webchat:
                case Channels.Sms:
                {
                    return await GetUser(userToken);
                }
                default: Debug.Fail("Missing channel type"); break;
            }

            return null;
        }

        /// <summary>
        /// Gets a user from a phone number.
        /// </summary>
        public Task<User> GetUser(string phoneNumber)
        {
            var user = this.client.CreateDocumentQuery<User>(
                UriFactory.CreateDocumentCollectionUri(
                    this.config.CosmosDatabase(),
                    this.config.CosmosUsersCollection()))
                .Where(u => u.PhoneNumber == phoneNumber)
                .AsEnumerable()
                .FirstOrDefault();

            return Task.FromResult(user);
        }

        /// <summary>
        /// Gets all user within a distance from coordinates.
        /// </summary>
        public Task<List<User>> GetUsersWithinDistance(Point coordinates, double distanceMeters)
        {
            var result = this.client.CreateDocumentQuery<User>(
                UriFactory.CreateDocumentCollectionUri(
                    this.config.CosmosDatabase(),
                    this.config.CosmosUsersCollection()),
                GetPartitionedFeedOptions())
                .Where(u => u.LocationCoordinates.Distance(coordinates) <= distanceMeters)
                .ToList();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets all user within a distance from coordinates that also match the provided phone numbers.
        /// </summary>
        public Task<List<User>> GetUsersWithinDistance(Point coordinates, double distanceMeters, List<string> phoneNumbers)
        {
            var result = this.client.CreateDocumentQuery<User>(
                UriFactory.CreateDocumentCollectionUri(
                    this.config.CosmosDatabase(),
                    this.config.CosmosUsersCollection()),
                GetPartitionedFeedOptions())
                .Where(u => phoneNumbers.Contains(u.PhoneNumber) && u.LocationCoordinates.Distance(coordinates) <= distanceMeters)
                .ToList();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets a resource for a user.
        /// </summary>
        public Task<Resource> GetResourceForUser(User user, string category, string resource)
        {
            var result = this.client.CreateDocumentQuery<Resource>(
                UriFactory.CreateDocumentCollectionUri(
                    this.config.CosmosDatabase(),
                    this.config.CosmosResourcesCollection()),
                GetPartitionedFeedOptions())
                .Where(r => r.CreatedById == user.Id && r.Category == category && r.Name == resource)
                .AsEnumerable()
                .FirstOrDefault();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets a need for a user.
        /// </summary>
        public Task<Need> GetNeedForUser(User user, string category, string resource)
        {
            var result = this.client.CreateDocumentQuery<Need>(
                UriFactory.CreateDocumentCollectionUri(
                    this.config.CosmosDatabase(),
                    this.config.CosmosNeedsCollection()),
                GetPartitionedFeedOptions())
                .Where(n => n.CreatedById == user.Id && n.Category == category && n.Name == resource)
                .AsEnumerable()
                .FirstOrDefault();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets a need from an ID.
        /// </summary>
        public Task<Need> GetNeedById(string id)
        {
            var result = this.client.CreateDocumentQuery<Need>(
                UriFactory.CreateDocumentCollectionUri(
                    this.config.CosmosDatabase(),
                    this.config.CosmosNeedsCollection()),
                GetPartitionedFeedOptions())
                .Where(n => n.Id == id)
                .AsEnumerable()
                .FirstOrDefault();

            return Task.FromResult(result);
        }

        private string GetCollection(Model model)
        {
            if (model is User)
            {
                return this.config.CosmosUsersCollection();
            }
            else if (model is Resource)
            {
                return this.config.CosmosResourcesCollection();
            }
            else if (model is Need)
            {
                return this.config.CosmosNeedsCollection();
            }
            else if (model is Feedback)
            {
                return this.config.CosmosFeedbackCollection();
            }
            else
            {
                Debug.Assert(false, "Add the new type");
                return string.Empty;
            }
        }

        private FeedOptions GetPartitionedFeedOptions()
        {
            // From https://docs.microsoft.com/en-us/azure/cosmos-db/performance-tips

            // If you don't know the number of partitions, you can set the degree of
            // parallelism to a high number. The system will choose the minimum
            // (number of partitions, user provided input) as the degree of parallelism.

            // When maxItemCount is set to -1, the SDK automatically finds the optimal
            // value, depending on the document size
            return new FeedOptions
            {
                EnableCrossPartitionQuery = true,
                MaxDegreeOfParallelism = 100,
                MaxItemCount = -1,
            };
        }
    }
}
