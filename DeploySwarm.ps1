# ===============================
# Step 0: Optional - Clean old containers/images
# ===============================
Write-Host "Removing old studentapp containers..."
docker ps -a --format "{{.ID}} {{.Image}}" | Where-Object { $_ -like "*studentapp*" } | ForEach-Object { docker rm -f ($_.Split()[0]) }

Write-Host "Removing old studentapp images..."
docker images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -like "*studentapp*" } | ForEach-Object { docker rmi -f $_ }

cd D:\DATA\RD\Docker\StudentApp
# ===============================
# Step 1: Build images in order
# ===============================
Write-Host "Building StudentApp.API..."
docker build -t studentapp-api:latest -f StudentApp.API\Dockerfile .

Write-Host "Building StudentApp.Worker..."
docker build -t studentapp-worker:latest  -f StudentApp.Worker\Dockerfile .

Write-Host "Building StudentApp.Notifications..."
docker build -t studentapp-notifications:latest  -f StudentApp.Notifications\Dockerfile .

Write-Host "Building StudentApp.Gateway..."
docker build -t studentapp-gateway:latest -f StudentApp.Gateway\Dockerfile .

# ===============================
# Step 2: Initialize swarm (if not already)
# ===============================
if (-not (docker info | Select-String "Swarm: active")) {
    Write-Host "Initializing Docker Swarm..."
    docker swarm init
}


# Remove old network if exists
if (docker network ls --format "{{.Name}}" | Select-String "studentapp_internal") {
    docker network rm studentapp_internal
}
# Remove old stack
docker stack rm studentapp


# ===============================
# Step 3: Deploy stack
# ===============================
Write-Host "Deploying StudentApp stack..."
docker stack deploy -c .\StudentApp\docker-stack.yml studentapp

# ===============================
# Step 4: Verify services
# ===============================
Write-Host "Waiting 10 seconds for services to start..."
Start-Sleep -Seconds 10

Write-Host "Listing StudentApp services..."
docker stack services studentapp

Write-Host "Done! Use 'docker service logs -f <service_name>' to check logs if needed."