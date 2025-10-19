using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ReportAISummary.API.Config;

namespace ReportAISummary.API.Utils
{
    public class AiUtils(
        Kernel kernel,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IPromptTemplateFactory promptTemplateFactory)
    {
        public static void PrepareServices(IServiceCollection services, IConfiguration config)
        {
            services.Configure<AISection>(config: config.GetSection(AISection.AISectionConfiguration));
            services.AddSingleton<Kernel>((sp) =>
            {
                var kernelBuilder = Kernel.CreateBuilder();
                var aiSection = sp.GetRequiredService<IOptions<AISection>>()!.Value;
                kernelBuilder.AddOpenAIChatClient(
                    modelId: aiSection.OPENAI_CHAT_MODEL,
                    apiKey: aiSection.OPENAI_API_KEY);
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                kernelBuilder.AddOpenAIEmbeddingGenerator(
                    serviceId: "emb",
                    modelId: aiSection.OPENAI_EMBED_MODEL,
                    apiKey: aiSection.OPENAI_API_KEY
                );
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                return kernelBuilder.Build();
            });

            services.AddSingleton(sp =>
            {
                var kernel = sp.GetRequiredService<Kernel>();
                return kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            });
            services.AddSingleton<IPromptTemplateFactory, KernelPromptTemplateFactory>();

            services.AddSingleton<AiUtils>();
        }

        public async Task<double[]> GetEmbeddingAsync(string text)
        {
            var vectors = await embeddingGenerator.GenerateAsync(value: text);
            var vector = vectors.Vector.ToArray();
            var resultedVector = new List<double>(vector.Length);
            for (int i = 0; i < vector.Length; i++)
            {
                resultedVector.Add(vector[i]);
            }
            return [.. resultedVector];
        }

        public async Task<string> SummarizeContent(string content, CancellationToken cancellationToken = default)
        {
            var effectiveKernel = kernel.Clone();

            var prompt = """
                Summarize the following document in a concise manner:

                {{$text}}

                """;
            var kernelArguments = new KernelArguments(
                    new OpenAIPromptExecutionSettings()
                    {
                        // no need yet
                        FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
                        Temperature = 0.1
                    })
            {
                ["text"] = content
            };
            var templateValue = promptTemplateFactory.Create(new PromptTemplateConfig(prompt));
            var renderedTemplate = await templateValue.RenderAsync(kernel, kernelArguments, cancellationToken);

            var summarizedContent = await effectiveKernel.InvokePromptAsync(
                renderedTemplate,
                arguments: kernelArguments,
                cancellationToken: cancellationToken);

            return summarizedContent.GetValue<string>()!;
        }
    }
}
