// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with EchoBot .NET Template version v4.11.1

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using PictureBot.Bots;

using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Azure.Blobs;
using Microsoft.PictureBot;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos;

namespace PictureBot
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        private ILoggerFactory _loggerFactory;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();

            // Create the Bot Framework Adapter with error handling enabled.
            services.AddSingleton<PictureBotAccessors>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;
                    if (options == null)
                    {
                        throw new InvalidOperationException("BotFrameworkOptions must be configured prior to setting up the state accessors");
                    }

                    var conversationState = sp.GetRequiredService<ConversationState>();
                    //var conversationState = services.BuildServiceProvider().GetService<ConversationState>();

                    if (conversationState == null)
                    {
                        throw new InvalidOperationException("ConversationState must be defined and added before adding conversation-scoped state accessors.");
                    }

                    // Create the custom state accessor.
                    // State accessors enable other components to read and write individual properties of state.
                    return new PictureBotAccessors(conversationState)
                    {
                        PictureState = conversationState.CreateProperty<PictureState>(PictureBotAccessors.PictureStateName),
                        DialogStateAccessor = conversationState.CreateProperty<DialogState>("DialogState"),
                    };

                });

            services.AddSingleton(sp =>
                {
                    var luisApplication = new LuisApplication(
                        Configuration.GetSection("luisAppId")?.Value,
                        Configuration.GetSection("luisAppKey")?.Value,
                        Configuration.GetSection("luisEndPoint")?.Value);
                        // Set the recognizer options depending on which endpoint version you want to use.
                        // More details can be found in https://docs.microsoft.com/en-gb/azure/cognitive-services/luis/luis-migration-api-v3
                        var recognizerOptions = new LuisRecognizerOptionsV3(luisApplication)
                        {
                            PredictionOptions = new Microsoft.Bot.Builder.AI.LuisV3.LuisPredictionOptions
                            {
                                IncludeAllIntents = true,
                            }
                        };
                    return new LuisRecognizer(recognizerOptions);
                });
            // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
            services.AddBot<PictureBot.Bots.PictureBot>(options =>
                {
                    var appId = Configuration.GetSection("MicrosoftAppId")?.Value;
                    var appSecret = Configuration.GetSection("MicrosoftAppPassword")?.Value;

                    options.CredentialProvider = new SimpleCredentialProvider(appId, appSecret);

                    // Creates a logger for the application to use.
                    ILogger logger = _loggerFactory.CreateLogger<PictureBot.Bots.PictureBot>();

                    // Catches any errors that occur during a conversation turn and logs them.
                    options.OnTurnError = async (context, exception) =>
                    {
                        logger.LogError($"Exception caught : {exception}");
                        await context.SendActivityAsync("Sorry, it looks like something went wrong.");
                    };


                    var middleware = options.Middleware;
                    // Add middleware below with "middleware.Add(...."
                    // Add Regex below
                    // middleware.Add(new RegExpRecognizerMiddleware()
                    //     .AddIntent("search", new Regex("search picture(?:s)*(.*)|search pic(?:s)*(.*)", RegexOptions.IgnoreCase))
                    //     .AddIntent("share", new Regex("share picture(?:s)*(.*)|share pic(?:s)*(.*)", RegexOptions.IgnoreCase))
                    //     .AddIntent("order", new Regex("order picture(?:s)*(.*)|order print(?:s)*(.*)|order pic(?:s)*(.*)", RegexOptions.IgnoreCase))
                    //     .AddIntent("help", new Regex("help(.*)", RegexOptions.IgnoreCase)));
                });
        
            // Create the User state.
            services.AddSingleton<UserState>(sp => {
                var dataStore = sp.GetRequiredService<IStorage>();
                    return new UserState(dataStore);
            });

            // Create the Conversation state.
            services.AddSingleton<ConversationState>(sp =>
            {
                var dataStore = sp.GetRequiredService<IStorage>();
                return new ConversationState(dataStore);
            });

            // Create the IStorage.
            services.AddSingleton<IStorage, BlobsStorage>(sp =>
            {
                var blobConnectionString = Configuration.GetSection("BlobStorageConnectionString")?.Value;
                var blobContainer = Configuration.GetSection("BlobStorageContainer")?.Value;
                BlobsStorage dataStore = new BlobsStorage(blobConnectionString, blobContainer);
                return dataStore;
            });
            services.AddSingleton(sp =>
                {
                    CosmosClientBuilder clientBuilder = new CosmosClientBuilder(
                        Configuration.GetSection("cosmosEndpointURI")?.Value,
                        Configuration.GetSection("cosmosKey")?.Value
                    );

                    CosmosClient client = clientBuilder
                        .WithConnectionModeDirect()
                        .Build();

                    Container container = client.GetContainer(
                        Configuration.GetSection("cosmosDatabaseName")?.Value,
                        Configuration.GetSection("cosmosCollectionName")?.Value
                    );

                    return container;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;

            app.UseDefaultFiles()
                        .UseBotFramework()
                        .UseStaticFiles()
                        .UseWebSockets()
                        .UseRouting()
                        .UseAuthorization()
                        .UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                        });
        }
    }
}
