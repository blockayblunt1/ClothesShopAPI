# Docker Setup for ClothesShop API

This document explains how to run the ClothesShop API using Docker.

## Prerequisites

- Docker Desktop installed and running
- Docker Compose (usually included with Docker Desktop)

## Quick Start with Docker Compose (Recommended)

The easiest way to run the entire application stack:

```bash
# Build and start both API and SQL Server
docker-compose up --build

# Or run in detached mode
docker-compose up --build -d
```

This will:
- Start a SQL Server 2022 container on port 1433
- Build and start the API container on port 5000
- Create a Docker network for communication between containers
- Set up a persistent volume for SQL Server data

### Access the Application

- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **SQL Server**: localhost:1433 (sa/YourStrong@Passw0rd)

## Individual Container Usage

### Build the API Docker Image

```bash
docker build -t clothesshop-api .
```

### Run the API Container

If you have an existing SQL Server instance:

```bash
docker run -p 5000:8080 \
  -e ConnectionStrings__DefaultConnection="Server=your-sql-server;Database=ClothesShopDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true" \
  clothesshop-api
```

## Database Setup

The API uses Entity Framework Core migrations. You may need to run migrations after the containers are up:

```bash
# If you have .NET CLI installed locally
dotnet ef database update

# Or execute inside the container
docker exec -it clothesshop-api dotnet ef database update
```

## Environment Variables

Key environment variables you can customize:

- `ASPNETCORE_ENVIRONMENT`: Set to `Development`, `Staging`, or `Production`
- `ConnectionStrings__DefaultConnection`: Database connection string
- `SA_PASSWORD`: SQL Server SA password (in docker-compose.yml)

## Stopping the Application

```bash
# Stop and remove containers
docker-compose down

# Stop and remove containers + volumes (removes database data)
docker-compose down -v
```

## Troubleshooting

### Port Conflicts
If port 5000 is already in use, modify the docker-compose.yml:
```yaml
ports:
  - "5001:8080"  # Change 5000 to 5001 or any available port
```

### SQL Server Connection Issues
- Ensure SQL Server container is fully started (may take 30-60 seconds)
- Check if port 1433 is available on your host machine
- Verify the connection string format and credentials

### Build Issues
Make sure you're running Docker commands from the root directory (where Dockerfile is located).