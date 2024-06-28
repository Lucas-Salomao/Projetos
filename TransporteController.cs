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

public class TransporteController
{
    // Variáveis de ambiente para configuração
    private readonly string _produtoApiUrl = Environment.GetEnvironmentVariable("PRODUTO_API_URL");
    private readonly string _sqsQueueUrl = Environment.GetEnvironmentVariable("SQS_QUEUE_URL");
    private readonly string _s3BucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");

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

    // Salva o pedido de transporte no DynamoDB
    private async Task SaveTransporte(Transporte transporte)
    {
        // Cria um cliente DynamoDB
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Cria um contexto DynamoDB
        using var dbContext = new DynamoDBContext(dbClient);

        // Salva o transporte no DynamoDB
        await dbContext.SaveAsync(transporte);
    }

    // Envia a mensagem para a fila do SQS
    private async Task SendSqsMessage(Transporte transporte)
    {
        // Cria um cliente SQS
        var sqsClient = new AmazonSQSClient(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Cria a mensagem para ser enviada
        var message = new SendMessageRequest
        {
            QueueUrl = _sqsQueueUrl,
            MessageBody = JsonSerializer.Serialize(transporte)
        };

        // Envia a mensagem para a fila
        await sqsClient.SendMessageAsync(message);
    }

    // Realiza a atualização do estoque na API de produtos
    private async Task UpdateEstoque(List<Produto> produtos)
    {
        // Cria um cliente HttpClient para realizar a requisição
        using var client = new HttpClient();

        // Itera sobre os produtos e atualiza o estoque na API de produtos
        foreach (var produto in produtos)
        {
            // Monta a URL da API de produtos
            var url = $"{_produtoApiUrl}/estoque/{produto.IdProduto}";

            // Cria o conteúdo da requisição
            var content = new StringContent(JsonSerializer.Serialize(new { quantidade = produto.Quantidade }), Encoding.UTF8, "application/json");

            // Realiza a requisição PATCH
            var response = await client.PatchAsync(url, content);

            // Verifica se a requisição foi bem-sucedida
            if (!response.IsSuccessStatusCode)
            {
                // Caso haja algum erro, retorna uma mensagem de erro
                throw new Exception($"Erro ao atualizar o estoque do produto: {response.StatusCode}");
            }
        }
    }

    // Salva os dados do envio no S3
    private async Task SaveToS3(Transporte transporte)
    {
        // Cria um cliente S3
        var s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Monta o objeto de upload para o S3
        var uploadRequest = new PutObjectRequest
        {
            BucketName = _s3BucketName,
            Key = $"transportes/{Guid.NewGuid()}.json",
            ContentBody = JsonSerializer.Serialize(transporte)
        };

        // Envia o objeto para o S3
        await s3Client.PutObjectAsync(uploadRequest);
    }

    private string GetRegionName() =>
        Environment.GetEnvironmentVariable("AWS_REGION") ?? "sa-east-1";

    public async Task<APIGatewayProxyResponse> CriarTransporte(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // Deserializa o payload da requisição em um objeto Transporte
            var transporte = JsonSerializer.Deserialize<Transporte>(request.Body);

            // Salva o pedido de transporte no DynamoDB
            await SaveTransporte(transporte);

            // Envia a mensagem para a fila do SQS
            await SendSqsMessage(transporte);

            // Retorna uma resposta de sucesso
            var response = GetDefaultResponse();
            response.Body = JsonSerializer.Serialize(new { Message = "Transporte criado com sucesso!" });
            return response;
        }
        catch (Exception ex)
        {
            // Caso haja algum erro, retorna uma resposta de erro
            var response = GetDefaultResponse();
            response.StatusCode = 500;
            response.Body = JsonSerializer.Serialize(new { Message = $"Erro ao criar transporte: {ex.Message}" });
            return response;
        }
    }

    public async Task<APIGatewayProxyResponse> FinalizarTransporte(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // Deserializa o payload da requisição em um objeto Transporte
            var transporte = JsonSerializer.Deserialize<Transporte>(request.Body);

            // Realiza a atualização do estoque na API de produtos
            await UpdateEstoque(transporte.Produtos);

            // Salva os dados do envio no S3
            await SaveToS3(transporte);

            // Retorna uma resposta de sucesso
            var response = GetDefaultResponse();
            response.Body = JsonSerializer.Serialize(new { Message = "Transporte finalizado com sucesso!" });
            return response;
        }
        catch (Exception ex)
        {
            // Caso haja algum erro, retorna uma resposta de erro
            var response = GetDefaultResponse();
            response.StatusCode = 500;
            response.Body = JsonSerializer.Serialize(new { Message = $"Erro ao finalizar transporte: {ex.Message}" });
            return response;
        }
    }
}

// Modelo de dados do transporte
public class Transporte
{
    public List<Produto> Produtos { get; set; }
    public string NomeLoja { get; set; }
}