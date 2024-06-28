using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Models;
using System.Net;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

public class BookControoler
{
    // Obtem a resposta padrão para as requisições da API
    private APIGatewayProxyResponse GetDefaultResponse()
    {
        var response = new APIGatewayProxyResponse()
        {
            Headers = new Dictionary<string, string>(),
            StatusCode = 200
        };

        // Define as cabeçalhos para permitir CORS
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Headers", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS, POST, GET, PUT, DELETE");
        response.Headers.Add("Content-Type", "application/json");

        return response;
    }

    // Obtém o nome da região do AWS
    private string GetRegionName() =>
        Environment.GetEnvironmentVariable("AWS_REGION") ?? "sa-east-1";

    // Salva um novo pedido
    public async Task<APIGatewayProxyResponse> SaveOrder(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Deserializa o pedido do corpo da requisição
        var order = JsonSerializer.Deserialize<Order>(request.Body);

        // Conecta ao DynamoDB
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));
        using (var dbContext = new DynamoDBContext(dbClient))
        {
            // Salva o pedido no DynamoDB
            await dbContext.SaveAsync(order);
        }

        // Retorna uma resposta de sucesso
        var response = GetDefaultResponse();
        response.Body = JsonSerializer.Serialize(new { Message = "Order saved successfully!" });
        return response;
    }

    // Obtém um pedido por ID
    public async Task<APIGatewayProxyResponse> GetOrderById(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Obtém o ID do pedido da URL
        var orderId = request.PathParameters["orderId"];

        // Conecta ao DynamoDB
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));
        using (var dbContext = new DynamoDBContext(dbClient))
        {
            // Busca o pedido pelo ID
            var order = await dbContext.LoadAsync<Order>(orderId);

            // Se o pedido for encontrado, retorna a resposta
            if (order != null)
            {
                var response = GetDefaultResponse();
                response.Body = JsonSerializer.Serialize(order);
                return response;
            }
        }

        // Se o pedido não for encontrado, retorna um erro 404
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.NotFound,
            Body = JsonSerializer.Serialize(new { Message = "Order not found" })
        };
    }

    // Atualiza um pedido existente
    public async Task<APIGatewayProxyResponse> UpdateOrder(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Obtém o ID do pedido da URL
        var orderId = request.PathParameters["orderId"];

        // Deserializa o pedido do corpo da requisição
        var updatedOrder = JsonSerializer.Deserialize<Order>(request.Body);

        // Conecta ao DynamoDB
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));
        using (var dbContext = new DynamoDBContext(dbClient))
        {
            // Busca o pedido pelo ID
            var order = await dbContext.LoadAsync<Order>(orderId);

            // Se o pedido for encontrado, atualiza os dados
            if (order != null)
            {
                order.Produtos = updatedOrder.Produtos;
                order.UrlTransportadora = updatedOrder.UrlTransportadora;
                await dbContext.SaveAsync(order);

                var response = GetDefaultResponse();
                response.Body = JsonSerializer.Serialize(new { Message = "Order updated successfully!" });
                return response;
            }
        }

        // Se o pedido não for encontrado, retorna um erro 404
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.NotFound,
            Body = JsonSerializer.Serialize(new { Message = "Order not found" })
        };
    }

    // Exclui um pedido
    public async Task<APIGatewayProxyResponse> DeleteOrder(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Obtém o ID do pedido da URL
        var orderId = request.PathParameters["orderId"];

        // Conecta ao DynamoDB
        var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(GetRegionName()));
        using (var dbContext = new DynamoDBContext(dbClient))
        {
            // Busca o pedido pelo ID
            var order = await dbContext.LoadAsync<Order>(orderId);

            // Se o pedido for encontrado, exclui do DynamoDB
            if (order != null)
            {
                await dbContext.DeleteAsync(order);

                var response = GetDefaultResponse();
                response.Body = JsonSerializer.Serialize(new { Message = "Order deleted successfully!" });
                return response;
            }
        }

        // Se o pedido não for encontrado, retorna um erro 404
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.NotFound,
            Body = JsonSerializer.Serialize(new { Message = "Order not found" })
        };
    }

    // Obtém o nome do produto por ID
    public async Task<APIGatewayProxyResponse> GetProductName(APIGatewayProxyRequest request, ILambdaContext context)
    {
        // Obtém o ID do produto da URL
        var productId = request.PathParameters["productId"];

        // Faz a requisição à API de produtos (substituir pela URL real)
        var productUrl = $"http://url.com/{productId}";
        var productResponse = await new HttpClient().GetAsync(productUrl);

        // Se a requisição à API de produtos for bem-sucedida
        if (productResponse.IsSuccessStatusCode)
        {
            // Deserializa o nome do produto da resposta da API
            var product = JsonSerializer.Deserialize<Product>(await productResponse.Content.ReadAsStringAsync());

            // Retorna o nome do produto
            var response = GetDefaultResponse();
            response.Body = JsonSerializer.Serialize(new { id_produto = productId, nome_produto = product.NomeProduto });
            return response;
        }

        // Se a requisição à API de produtos falhar, retorna um erro 500
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Body = JsonSerializer.Serialize(new { Message = "Error fetching product name" })
        };
    }
}

// Modelo do pedido
public class Order
{
    [DynamoDBHashKey]
    public string OrderId { get; set; }

    public List<ProductItem> Produtos { get; set; }
    public string UrlTransportadora { get; set; }
}

// Modelo do item de produto
public class ProductItem
{
    public string IdProduto { get; set; }
    public int Quantidade { get; set; }
}

// Modelo do produto (obtido da API de produtos)
public class Product
{
    public string IdProduto { get; set; }
    public string NomeProduto { get; set; }
}
