using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using loja.data;
using loja.models;
using loja.services;
using Microsoft.Extensions.Primitives;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes("abc"))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add services to the container.
builder.Services.AddDbContext<LojaDbContext>(options => 
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"), 
    new MySqlServerVersion(new Version(8, 0, 26))));

builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<FornecedorService>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<DepositoService>();
builder.Services.AddScoped<VendaService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

async Task<bool> ValidateTokenAsync(HttpContext context)
{
    if (!context.Request.Headers.ContainsKey("Authorization"))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Token não fornecido");
        return false;
    }

    var token = GetTokenFromHeader(context.Request.Headers["Authorization"]);
    var key = Encoding.ASCII.GetBytes("abcabcabcabcabcabcabcabcabcabcabc");
    var tokenHandler = new JwtSecurityTokenHandler();
    var validationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };

    try
    {
        tokenHandler.ValidateToken(token, validationParameters, out _);
        return true;
    }
    catch (Exception)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Token inválido");
        return false;
    }
}

string GetTokenFromHeader(StringValues stringValues)
{
    var headerValue = stringValues.ToString();
    if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return headerValue.Substring("Bearer ".Length).Trim();
    }
    return null;
}

// Usuário Endpoints
app.MapPost("/login", async (HttpContext context, UsuarioService usuarioService) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    var json = JsonDocument.Parse(body);
    var email = json.RootElement.GetProperty("email").GetString();
    var senha = json.RootElement.GetProperty("senha").GetString();

    var usuario = await usuarioService.GetUsuarioByEmailAndSenhaAsync(email, senha);

    if (usuario != null)
    {
        var token = usuarioService.GenerateToken(email);
        await context.Response.WriteAsync(token);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Email ou senha inválidos.");
    }
});

app.MapPost("/registro", async (Usuario usuario, UsuarioService userService) =>
{
    await userService.RegisterUserAsync(usuario);
    return Results.Created($"/registro/{usuario.Id}", usuario);
});

app.MapGet("/rotaSegura", async (HttpContext context) =>
{
if (!context.Request.Headers.ContainsKey("Authorization"))
{
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsync("Token não fornecido");
    return;
}

var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
var tokenHandler = new JwtSecurityTokenHandler();
var key = Encoding.ASCII.GetBytes("abcabcabcabcabcabcabcabcabcabcabc");
var validationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(key),
    ValidateIssuer = false,
    ValidateAudience = false
};

try
{
    tokenHandler.ValidateToken(token, validationParameters, out _);
}
catch (Exception)
{
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsync("Token inválido");
    return;
}

await context.Response.WriteAsync("Autorizado");
});

app.MapGet("/usuarios", async (HttpContext context,UsuarioService usuarioService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var usuarios = await usuarioService.GetAllUsuariosAsync();
        await context.Response.WriteAsync(JsonSerializer.Serialize(usuarios));
    }
});

app.MapGet("/usuarios/{id}", async (HttpContext context, int id, UsuarioService usuarioService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var usuario = await usuarioService.GetUsuarioByIdAsync(id);
        if (usuario == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"Usuário com o ID {id} não encontrado.");
            return;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(usuario));
    }
});

app.MapPut("/usuarios/{id}", async (HttpContext context, int id, Usuario updatedUsuario, UsuarioService usuarioService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var existingUsuario = await usuarioService.GetUsuarioByIdAsync(id);
        if (existingUsuario == null)
        {
            return Results.NotFound($"Usuario with ID {id} not found.");
        }

        existingUsuario.Nome = updatedUsuario.Nome;
        existingUsuario.Email = updatedUsuario.Email;
        existingUsuario.Senha = updatedUsuario.Senha;

        await usuarioService.UpdateUsuarioAsync(existingUsuario);
        return Results.Ok(existingUsuario);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});


app.MapDelete("/usuarios/{id}", async (HttpContext context, int id, UsuarioService usuarioService) =>
{
    if (await ValidateTokenAsync(context))
    {
    await usuarioService.DeleteUsuarioAsync(id);
    return Results.Ok();
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

// Produtos Endpoints
app.MapPost("/produtos", async (HttpContext context, Produto produto, ProductService productService) =>
{
    if (await ValidateTokenAsync(context))
    {
    await productService.AddProductAsync(produto);
    return Results.Created($"/produtos/{produto.Id}", produto);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/produtos", async (HttpContext context, ProductService productService) =>
{
    if (await ValidateTokenAsync(context))
    {
    var produtos = await productService.GetAllProductsAsync();
    return Results.Ok(produtos);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/produtos/{id}", async (HttpContext context, int id, ProductService productService) =>
{
    if (await ValidateTokenAsync(context))
    {
    var produto = await productService.GetProductByIdAsync(id);
    if (produto == null)
    {
        return Results.NotFound($"Product with ID {id} not found.");
    }
    return Results.Ok(produto);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapPut("/produtos/{id}", async (HttpContext context, int id, Produto updatedProduto, ProductService productService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var existingProduct = await productService.GetProductByIdAsync(id);
        if (existingProduct == null)
        {
            return Results.NotFound($"Product with ID {id} not found.");
        }

        existingProduct.Nome = updatedProduto.Nome;
        existingProduct.Preco = updatedProduto.Preco;
        existingProduct.Quantidade = updatedProduto.Quantidade;
        existingProduct.DepositoId = updatedProduto.DepositoId;

        await productService.UpdateProductAsync(existingProduct);
        return Results.Ok(existingProduct);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});



app.MapDelete("/produtos/{id}", async (HttpContext context, int id, ProductService productService) =>
{
    if (await ValidateTokenAsync(context))
    {
        await productService.DeleteProductAsync(id);
        return Results.Ok();
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

// Cliente Endpoints
app.MapPost("/clientes", async (HttpContext context, Cliente cliente, ClienteService clienteService) =>
{
    if (await ValidateTokenAsync(context))
    {
        await clienteService.AddClienteAsync(cliente);
        return Results.Created($"/clientes/{cliente.Id}", cliente);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/clientes", async (HttpContext context, ClienteService clienteService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var clientes = await clienteService.GetAllClientesAsync();
        return Results.Ok(clientes);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/clientes/{id}", async (HttpContext context, int id, ClienteService clienteService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var cliente = await clienteService.GetClienteByIdAsync(id);
        if (cliente == null)
        {
            return Results.NotFound($"Cliente with ID {id} not found.");
        }
        return Results.Ok(cliente);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapPut("/clientes/{id}", async (HttpContext context, int id, Cliente updatedCliente, ClienteService clienteService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var existingCliente = await clienteService.GetClienteByIdAsync(id);
        if (existingCliente == null)
        {
            return Results.NotFound($"Cliente with ID {id} not found.");
        }

        existingCliente.Nome = updatedCliente.Nome;
        existingCliente.Cpf = updatedCliente.Cpf;
        existingCliente.Email = updatedCliente.Email;

        await clienteService.UpdateClienteAsync(existingCliente);
        return Results.Ok(existingCliente);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});


app.MapDelete("/clientes/{id}", async (HttpContext context, int id, ClienteService clienteService) =>
{
    if (await ValidateTokenAsync(context))
    {
        await clienteService.DeleteClienteAsync(id);
        return Results.Ok();
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

// Fornecedor Endpoints
app.MapPost("/fornecedores", async (HttpContext context, Fornecedor fornecedor, FornecedorService fornecedorService) =>
{
    if (await ValidateTokenAsync(context))
    {
        await fornecedorService.AddFornecedorAsync(fornecedor);
        return Results.Created($"/fornecedores/{fornecedor.Id}", fornecedor);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/fornecedores", async (HttpContext context, FornecedorService fornecedorService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var fornecedores = await fornecedorService.GetAllFornecedoresAsync();
        return Results.Ok(fornecedores);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/fornecedores/{Id}", async (HttpContext context, int id, FornecedorService fornecedorService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var fornecedor = await fornecedorService.GetFornecedorByIdAsync(id);
        if (fornecedor == null)
        {
            return Results.NotFound($"Fornecedor with ID {id} not found.");
        }
        return Results.Ok(fornecedor);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapPut("/fornecedores/{Id}", async (HttpContext context, int id, Fornecedor updatedFornecedor, FornecedorService fornecedorService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var existingFornecedor = await fornecedorService.GetFornecedorByIdAsync(id);
        if (existingFornecedor == null)
        {
            return Results.NotFound($"Fornecedor with ID {id} not found.");
        }

        existingFornecedor.Cnpj = updatedFornecedor.Cnpj;
        existingFornecedor.Nome = updatedFornecedor.Nome;
        existingFornecedor.Endereco = updatedFornecedor.Endereco;
        existingFornecedor.Email = updatedFornecedor.Email;
        existingFornecedor.Telefone = updatedFornecedor.Telefone;

        await fornecedorService.UpdateFornecedorAsync(existingFornecedor);
        return Results.Ok(existingFornecedor);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});


app.MapDelete("/fornecedores/{Id}", async (HttpContext context, int id, FornecedorService fornecedorService) =>
{
    if (await ValidateTokenAsync(context))
    {
        await fornecedorService.DeleteFornecedorAsync(id);
        return Results.Ok();
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

//Deposito Endpoints

app.MapPost("/depositos", async (HttpContext context, Deposito deposito, DepositoService depositoService) =>
{
    if (await ValidateTokenAsync(context))
    {
        await depositoService.AddDepositoAsync(deposito);
        return Results.Created($"/depositos/{deposito.Id}", deposito);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/depositos", async (HttpContext context, DepositoService depositoService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var depositos = await depositoService.GetAllDepositosAsync();
        return Results.Ok(depositos);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/depositos/{id}", async (HttpContext context, int id, DepositoService depositoService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var deposito = await depositoService.GetDepositoByIdAsync(id);
        if (deposito == null)
        {
            return Results.NotFound($"Deposito with ID {id} not found.");
        }
        return Results.Ok(deposito);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapPut("/depositos/{id}", async (HttpContext context, int id, Deposito updatedDeposito, DepositoService depositoService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var existingDeposito = await depositoService.GetDepositoByIdAsync(id);
        if (existingDeposito == null)
        {
            return Results.NotFound($"Deposito with ID {id} not found.");
        }

        existingDeposito.Nome = updatedDeposito.Nome;

        await depositoService.UpdateDepositoAsync(existingDeposito);
        return Results.Ok(existingDeposito);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});


app.MapDelete("/depositos/{id}", async (HttpContext context, int id, DepositoService depositoService) =>
{
    if (await ValidateTokenAsync(context))
    {
        await depositoService.DeleteDepositoAsync(id);
        return Results.Ok();
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

//Venda Endpoints

app.MapPost("/vendas", async (HttpContext context, Venda venda, VendaService vendaService) =>
{
    if (await ValidateTokenAsync(context))
    {
        try
        {
            await vendaService.AddVendaAsync(venda);
            return Results.Created($"/vendas/{venda.Id}", venda);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/vendas", async (HttpContext context, VendaService vendaService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var vendas = await vendaService.GetAllVendasAsync();
        return Results.Ok(vendas);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/vendas/{id}", async (HttpContext context, int id, VendaService vendaService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var venda = await vendaService.GetVendaByIdAsync(id);
        if (venda == null)
        {
            return Results.NotFound($"Venda with ID {id} not found.");
        }
        return Results.Ok(venda);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapPut("/vendas/{id}", async (HttpContext context, int id, Venda updatedVenda, VendaService vendaService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var existingVenda = await vendaService.GetVendaByIdAsync(id);
        if (existingVenda == null)
        {
            return Results.NotFound($"Venda with ID {id} not found.");
        }

        existingVenda.DataVenda = updatedVenda.DataVenda;
        existingVenda.NumeroNotaFiscal = updatedVenda.NumeroNotaFiscal;
        existingVenda.ClienteId = updatedVenda.ClienteId;
        existingVenda.ProdutoId = updatedVenda.ProdutoId;
        existingVenda.DepositoId = updatedVenda.DepositoId;
        existingVenda.Quantidade = updatedVenda.Quantidade;
        existingVenda.PrecoUnitario = updatedVenda.PrecoUnitario;

        await vendaService.UpdateVendaAsync(existingVenda);
        return Results.Ok(existingVenda);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});


app.MapDelete("/vendas/{id}", async (HttpContext context, int id, VendaService vendaService) =>
{
    if (await ValidateTokenAsync(context))
    {
        await vendaService.DeleteVendaAsync(id);
        return Results.Ok();
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/vendas/produto/{produtoId}/detalhadas", async (HttpContext context, int produtoId, VendaService vendaService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var vendasDetalhadas = await vendaService.GetVendasPorProdutoDetalhadasAsync(produtoId);
        if (vendasDetalhadas == null || !vendasDetalhadas.Any())
        {
            return Results.NotFound($"No sales details found for product ID {produtoId}.");
        }
        return Results.Ok(vendasDetalhadas);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/vendas/produto/{id}/sumarizada", async (HttpContext context, int id, VendaService vendaService) =>
{
    if (await ValidateTokenAsync(context))
    {
        return Results.Ok(await vendaService.GetVendasPorProdutoSumarizadasAsync(id));
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/vendas/cliente/{clienteId}/detalhadas", async (HttpContext context, int clienteId, VendaService vendaService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var vendasDetalhadas = await vendaService.GetVendasPorClienteDetalhadasAsync(clienteId);
        if (vendasDetalhadas == null || !vendasDetalhadas.Any())
        {
            return Results.NotFound($"No sales details found for client ID {clienteId}.");
        }
        return Results.Ok(vendasDetalhadas);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/vendas/cliente/{id}/sumarizada", async (HttpContext context, int id, VendaService vendaService) =>
{
    if (await ValidateTokenAsync(context))
    {
        return Results.Ok(await vendaService.GetVendasPorClienteSumarizadasAsync(id));
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/deposito/{depositoId}/sumarizada", async (HttpContext context, int depositoId , DepositoService depositoService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var result = await depositoService.GetProdutosNoDepositoSumarizadaAsync(depositoId);
        return Results.Ok(result);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

app.MapGet("/produtos/{id}/quantidade", async (HttpContext context, int id, ProductService productService) =>
{
    if (await ValidateTokenAsync(context))
    {
        var produto = await productService.GetProductByIdAsync(id);
        if (produto == null)
        {
            return Results.NotFound($"Produto com ID {id} não encontrado.");
        }

        var result = new 
        {
            Nome = produto.Nome,
            Quantidade = produto.Quantidade
        };

        return Results.Ok(result);
    }
    else
    {
        return Results.BadRequest("O token de autenticação é inválido ou expirou.");
    }
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
