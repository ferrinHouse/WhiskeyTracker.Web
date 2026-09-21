# Deployment Guide

## ☸️ Kubernetes Deployment

This project uses **Kustomize** for configuration management and deploys a **multi-arch container image** (`linux/amd64` + `linux/arm64`) published to GitHub Container Registry.

### Prerequisites
1.  **Kubernetes Cluster**: Ensure your cluster is running.
2.  **NFS Server**: You need an NFS server for persistent storage.
    *   **Photos**: `/export/whiskey-photos` (For user uploads)
    *   **Backups**: `/export/whiskey-app` (Pre-migration database dumps, under `backups/`)

### 📦 Image Deployment Strategy
On every push to `main`, CI:
1.  Runs the tests.
2.  Builds a multi-arch image and pushes it to `ghcr.io/ferrinhouse/whiskeytracker.web`, tagged `sha-<full commit sha>` and `latest`. The package is public, so the cluster needs no pull secret.
3.  On the self-hosted runner, checks for pending EF migrations and, if there are any, dumps the database to `/app/backups` on the NFS backup share **before** the new version starts (the app applies migrations on startup).
4.  Applies the manifests with the image tag set to the commit SHA and waits for the rollout (`kubectl rollout status`). The old pod keeps serving until the new one passes its readiness probe, and the job fails if it never does.

The pod prefers arm64 nodes but can run on any node. Every node that can run it needs `nfs-common` installed for the photo volume.

To roll back, run `kubectl rollout undo deployment/whiskey-web`, or re-run the pipeline for an earlier commit. Note that database migrations are not reversed.

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
2.  Update the IP for `whiskey-db-pv`, `whiskey-photos-pv`, and `whiskey-app-pv` (the backup share).

### 🚀 Manual Deployment
If you need to deploy manually from the Pi:
1.  **Code Updates**: Manifests reference the `latest` image by default. To deploy a specific build, set the tag first, run `kubectl set image deployment/whiskey-web whiskey-web=ghcr.io/ferrinhouse/whiskeytracker.web:sha-<commit>` after applying.
2.  **Infrastructure**:
    ```bash
    # Create secrets file
echo "# TODO: Populate this file with secrets as described in the 'Managing App Secrets' section" > k8s/.env
    
    # Apply K8s manifests
    kubectl apply -k k8s/
    
    # Wait for the app to become ready
    kubectl rollout status deployment/whiskey-web
    ```
