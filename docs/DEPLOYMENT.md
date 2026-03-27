# Deployment Guide

## ☸️ Kubernetes Deployment

This project uses **Kustomize** for configuration management and a **Binary Deployment** strategy on external NFS storage.

### Prerequisites
1.  **Kubernetes Cluster**: Ensure your cluster is running.
2.  **NFS Server**: You need an NFS server for persistent storage.
    *   **Photos**: `/export/whiskey-photos` (For user uploads)
    *   **App Code**: `/export/whiskey-app` (For application binaries)

### 📦 Binary Deployment Strategy
We do **not** build custom Docker images for each release. Instead:
1.  CI compiles the .NET application.
2.  Binaries are copied to the **NFS App Share** (`/export/whiskey-app`).
3.  Pods run a generic runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0`) and mount the code from NFS.
4.  Deployment is restarted to load the new binaries.

### 🔐 Managing App Secrets
We use a **SecretGenerator** strategy. The CI pipeline generates a versioned secret (e.g., `whiskey-secrets-h5k2`) automatically on every deploy.

**Required Secrets (GitHub):**
Configure `SECRETS_ENV_FILE` in GitHub with the following content:
```env
# Database Credentials
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_super_secret_password
POSTGRES_DB=whiskey_prod

# Connection String
connection-string=Host=postgres-service;Database=whiskey_prod;Username=postgres;Password=your_super_secret_password

# Authentication (Google)
Authentication__Google__ClientId=your_google_client_id
Authentication__Google__ClientSecret=your_google_client_secret

# Email
EmailSettings__Host=smtp.gmail.com
EmailSettings__Port=587
EmailSettings__User=your_email@gmail.com
EmailSettings__Password=your_app_password
```

### 🌍 Networking (NodePort)
The application is exposed via a **NodePort** service on port `30080`.
Typically, you will use a reverse proxy (like Nginx Proxy Manager) to forward traffic from `whiskeytracker.ferrinhouse.org` to `NODE_IP:30080`.

### 💾 Storage Configuration
Storage is defined in `k8s/storage.yaml`.
The NFS Server IP is injected via `k8s/patches/nfs-server.yaml`.

**To change the NFS IP:**
1.  Edit `k8s/patches/nfs-server.yaml`.
2.  Update the IP for both `whiskey-db-pv`, `whiskey-photos-pv`, and `whiskey-app-pv`.

### 🚀 Manual Deployment
If you need to deploy manually from the Pi:
1.  **Code Updates**: You must manually copy published binaries to your NFS server at `/export/whiskey-app`.
2.  **Infrastructure**:
    ```bash
    # Create secrets file
echo "# TODO: Populate this file with secrets as described in the 'Managing App Secrets' section" > k8s/.env
    
    # Apply K8s manifests
    kubectl apply -k k8s/
    
    # Restart app to pick up code changes
    kubectl rollout restart deployment/whiskey-web
    ```
