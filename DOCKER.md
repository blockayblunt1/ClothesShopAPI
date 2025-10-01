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
- Start a PostgreSQL 17 container on port 5432
- Build and start the API container on port 5000
- Create a Docker network for communication between containers
- Set up a persistent volume for PostgreSQL data

### Access the Application

- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **PostgreSQL**: localhost:5432 (postgres/postgres)

## Individual Container Usage

### Build the API Docker Image

```bash
docker build -t clothesshop-api .
```

### Run the API Container

If you have an existing PostgreSQL instance:

```bash
docker run -p 5000:8080 \
  -e ConnectionStrings__DefaultConnection="Host=your-postgres-host;Port=5432;Database=ClothesShopDB;Username=postgres;Password=YourPassword;Include Error Detail=true" \
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
- `POSTGRES_PASSWORD`: PostgreSQL password (in docker-compose.yml)

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

### PostgreSQL Connection Issues
- Ensure PostgreSQL container is fully started (may take 10-20 seconds)
- Check if port 5432 is available on your host machine
- Verify the connection string format and credentials
- Check PostgreSQL logs: `docker logs clothesshop-postgres`

### Build Issues
Make sure you're running Docker commands from the root directory (where Dockerfile is located).