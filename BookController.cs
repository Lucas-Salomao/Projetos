using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Models;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

public class BookController
{
    // Define o nome da tabela do DynamoDB para armazenar os livros.
    private const string TABLE_NAME = "Books";

    // Cria uma resposta padrão para as requisições do API Gateway.
    private APIGatewayProxyResponse GetDefaultResponse()
    {
        var response = new APIGatewayProxyResponse()
        {
            Headers = new Dictionary<string, string>(),
            StatusCode = 200
        };

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Headers", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS, GET, POST, PUT, DELETE");
        response.Headers.Add("Content-Type", "application/json");

        return response;
    }

    // Obtém o nome da região do AWS.
    private string GetRegionName() =>
        Environment.GetEnvironmentVariable("AWS_REGION") ?? "sa-east-1";

    // Salva um novo livro na tabela do DynamoDB.
    public async Task<APIGatewayProxyResponse> SaveBook(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Deserializa o objeto 'Book' a partir do corpo da requisição.
        var book = JsonSerializer.Deserialize<Book>(request.Body);

        // Cria um cliente para o DynamoDB.
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Cria um contexto para interagir com o DynamoDB.
        using (var dbContext = new DynamoDBContext(dbClient))
        {
            // Salva o livro na tabela.
            await dbContext.SaveAsync(book);
        }

        // Cria uma resposta com a mensagem de sucesso.
        var response = GetDefaultResponse();
        response.Body = JsonSerializer.Serialize(new { Message = "Book saved successfully!" });

        // Retorna a resposta.
        return response;
    }

    // Obtém um livro específico da tabela do DynamoDB pelo seu ID.
    public async Task<APIGatewayProxyResponse> GetBook(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Obtém o ID do livro a partir do parâmetro de caminho da requisição.
        var bookId = request.PathParameters["bookId"];

        // Cria um cliente para o DynamoDB.
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Cria um contexto para interagir com o DynamoDB.
        using (var dbContext = new DynamoDBContext(dbClient))
        {
            // Busca o livro pelo seu ID.
            var book = await dbContext.LoadAsync<Book>(bookId);

            // Se o livro for encontrado, retorna uma resposta com o livro.
            if (book != null)
            {
                var response = GetDefaultResponse();
                response.Body = JsonSerializer.Serialize(book);
                return response;
            }
        }

        // Se o livro não for encontrado, retorna uma resposta com status 404 (Not Found).
        return new APIGatewayProxyResponse { StatusCode = 404 };
    }

    // Atualiza um livro existente na tabela do DynamoDB.
    public async Task<APIGatewayProxyResponse> UpdateBook(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Obtém o ID do livro a partir do parâmetro de caminho da requisição.
        var bookId = request.PathParameters["bookId"];

        // Deserializa o objeto 'Book' a partir do corpo da requisição.
        var updatedBook = JsonSerializer.Deserialize<Book>(request.Body);

        // Cria um cliente para o DynamoDB.
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Cria um contexto para interagir com o DynamoDB.
        using (var dbContext = new DynamoDBContext(dbClient))
        {
            // Atualiza o livro na tabela.
            await dbContext.SaveAsync(updatedBook);
        }

        // Cria uma resposta com a mensagem de sucesso.
        var response = GetDefaultResponse();
        response.Body = JsonSerializer.Serialize(new { Message = "Book updated successfully!" });

        // Retorna a resposta.
        return response;
    }

    // Exclui um livro da tabela do DynamoDB pelo seu ID.
    public async Task<APIGatewayProxyResponse> DeleteBook(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Obtém o ID do livro a partir do parâmetro de caminho da requisição.
        var bookId = request.PathParameters["bookId"];

        // Cria um cliente para o DynamoDB.
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Cria um contexto para interagir com o DynamoDB.
        using (var dbContext = new DynamoDBContext(dbClient))
        {
            // Exclui o livro da tabela.
            await dbContext.DeleteAsync<Book>(bookId);
        }

        // Cria uma resposta com a mensagem de sucesso.
        var response = GetDefaultResponse();
        response.Body = JsonSerializer.Serialize(new { Message = "Book deleted successfully!" });

        // Retorna a resposta.
        return response;
    }

    // Obtém uma lista de todos os livros da tabela do DynamoDB.
    public async Task<APIGatewayProxyResponse> GetAllBooks(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Cria um cliente para o DynamoDB.
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));

        // Cria um contexto para interagir com o DynamoDB.
        using (var dbContext = new DynamoDBContext(dbClient))
        {
            // Define a consulta para buscar todos os livros.
            var query = dbContext.QueryAsync<Book>(new DynamoDBOperationConfig { OverrideTableName = TABLE_NAME });

            // Itera sobre os resultados da consulta.
            var books = new List<Book>();
            do
            {
                var response = await query.GetNextSetAsync();
                books.AddRange(response);
            } while (!query.IsDone);

            // Cria uma resposta com a lista de livros.
            var response = GetDefaultResponse();
            response.Body = JsonSerializer.Serialize(books);

            // Retorna a resposta.
            return response;
        }
    }
}
