using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SQS;
using Amazon.SQS.Model;
using Models;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

public class PedidoController
{
    // Variáveis de ambiente para configuração
    private readonly string _produtoApiUrl = Environment.GetEnvironmentVariable("PRODUTO_API_URL");
    private readonly string _sqsQueueUrl = Environment.GetEnvironmentVariable("SQS_QUEUE_URL");
    private readonly string _s3BucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");

    private readonly string _nomeLoja = "Minha Loja"; // Nome da sua loja

    private APIGatewayProxyResponse GetDefaultResponse()
    {
        // Define a resposta padrão com headers de CORS
        var response = new APIGatewayProxyResponse()
        {
            Headers = new Dictionary<string, string>(),
            StatusCode = 200
        };

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Headers", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS, POST");
        response.Headers.Add("Content-Type", "application/json");

        return response;
    }

    // Obtem o nome do produto da API de produtos
    private async Task<string> GetProductName(string idProduto)
    {
        // Cria um cliente HttpClient para realizar a requisição
        using var client = new HttpClient();

        // Monta a URL da API de produtos
        var url = $"{_produtoApiUrl}/{idProduto}";

        // Realiza a requisição GET
        var response = await client.GetAsync(url);

        // Verifica se a requisição foi bem-sucedida
        if (response.IsSuccessStatusCode)
        {
            // Deserializa a resposta JSON em um objeto Produto
            var produto = await response.Content.ReadFromJsonAsync<Produto>();

            // Retorna o nome do produto
            return produto.NomeProduto;
        }
        else
        {
            // Caso haja algum erro, retorna uma mensagem de erro
            throw new Exception($"Erro ao obter o nome do produto: {response.StatusCode}");
        }
    }

    // Salva o pedido no DynamoDB
    private async Task SavePedido(Pedido pedido)
    {
        // Cria um cliente DynamoDB
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Cria um contexto DynamoDB
        using var dbContext = new DynamoDBContext(dbClient);

        // Salva o pedido no DynamoDB
        await dbContext.SaveAsync(pedido);
    }

    // Envia a mensagem para a fila do SQS
    private async Task SendSqsMessage(Pedido pedido)
    {
        // Cria um cliente SQS
        var sqsClient = new AmazonSQSClient(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Cria a mensagem para ser enviada
        var message = new SendMessageRequest
        {
            QueueUrl = _sqsQueueUrl,
            MessageBody = JsonSerializer.Serialize(pedido)
        };

        // Envia a mensagem para a fila
        await sqsClient.SendMessageAsync(message);
    }

    // Salva os dados do pedido e do envio no S3
    private async Task SaveToS3(Pedido pedido, string transportadoraUrl)
    {
        // Cria um cliente S3
        var s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Monta o objeto de upload para o S3
        var uploadRequest = new PutObjectRequest
        {
            BucketName = _s3BucketName,
            Key = $"pedidos/{Guid.NewGuid()}.json",
            ContentBody = JsonSerializer.Serialize(new
            {
                pedido,
                transportadoraUrl
            })
        };

        // Envia o objeto para o S3
        await s3Client.PutObjectAsync(uploadRequest);
    }

    private string GetRegionName() =>
        Environment.GetEnvironmentVariable("AWS_REGION") ?? "sa-east-1";

    public async Task<APIGatewayProxyResponse> CriarPedido(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // Deserializa o payload da requisição em um objeto Pedido
            var pedido = JsonSerializer.Deserialize<Pedido>(request.Body);

            // Obtem o nome dos produtos da API de produtos
            foreach (var produto in pedido.Produtos)
            {
                produto.NomeProduto = await GetProductName(produto.IdProduto.ToString());
            }

            // Salva o pedido no DynamoDB
            await SavePedido(pedido);

            // Envia a mensagem para a fila do SQS
            await SendSqsMessage(pedido);

            // Salva os dados do pedido e do envio no S3
            await SaveToS3(pedido, pedido.UrlTransportadora);

            // Retorna uma resposta de sucesso
            var response = GetDefaultResponse();
            response.Body = JsonSerializer.Serialize(new { Message = "Pedido criado com sucesso!" });
            return response;
        }
        catch (Exception ex)
        {
            // Caso haja algum erro, retorna uma resposta de erro
            var response = GetDefaultResponse();
            response.StatusCode = 500;
            response.Body = JsonSerializer.Serialize(new { Message = $"Erro ao criar pedido: {ex.Message}" });
            return response;
        }
    }
}

// Modelo de dados do pedido
public class Pedido
{
    public List<Produto> Produtos { get; set; }
    public string UrlTransportadora { get; set; }
}

// Modelo de dados do produto
public class Produto
{
    public int IdProduto { get; set; }
    public int Quantidade { get; set; }
    public string NomeProduto { get; set; }
}
