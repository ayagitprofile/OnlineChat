await using var server = new Server.Server(port: 5000);

await server.Run();