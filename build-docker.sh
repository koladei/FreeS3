#!/bin/bash
set -e  # stop on first error

# ============================
# ASP.NET Core Microservices Docker Build Script
#
# Usage:
#   ./build-docker.sh [--registry <registry-or-repository>] [--service <*|S3Server,Service2>] [--platform <all|amd64|arm64>] [--deploy [<path-to-docker-compose-file>]]
#
# Options:
#   --registry   Registry host or Docker repository base (default: taviatech.azurecr.io)
#   --service    Comma-separated services to build (default: * = all services)
#   --platform   Which platform(s) to build: all|amd64|arm64 (default: all)
#   --deploy     Optional: path to a docker-compose file to run after push.
#                If provided with no value, defaults to ./docker-compose.yml
#                Paths are resolved relative to this script's directory.
#   --no-pull    Skip running 'git pull' at the start of the script.
#   --no-push    Build images locally but skip pushing them to the registry.
#   --only-push  Skip build steps and only push existing local images to the registry.
#   --production Use production tags (latest, latest-arm). Default uses dev tags (latest-dev, latest-arm-dev).
#   --help,-h    Show this help message.
#
# TAB COMPLETION:
#   source ./build-docker-completion.sh
#   (add to ~/.zshrc or ~/.bashrc to make it permanent)
#
# Defaults:
#   registry = taviatech.azurecr.io
#   service  = *  (all services)
#   platform = all (amd64 + arm64)
#   deploy   = (not set)
# ============================

# Default values
registry="taviatech.azurecr.io"
services_arg="*"
deploy=""
no_pull=""
no_push=""
only_push=""
production=""
platforms="all"

# Parse named parameters
while [[ $# -gt 0 ]]; do
  case $1 in
    --registry)
      registry="$2"
      shift 2
      ;;
    --service)
      services_arg="$2"
      shift 2
      ;;
    --platform)
      platforms="$2"
      shift 2
      ;;
    --help|-h)
      echo "Usage: $0 [--registry <registry-or-repository>] [--service <*|S3Server,OtherService>] [--platform <all|amd64|arm64>] [--deploy [<path-to-docker-compose-file>]] [--no-pull] [--no-push] [--only-push] [--production]"
      echo ""
      echo "If --deploy is provided with no value the default compose path './docker-compose.yml' will be used."
      echo "If --registry is a host like localhost:5000 or ghcr.io/org, images are tagged as <registry>/<service>:<tag>."
      echo "If --registry is a Docker Hub repository like koladei/frees3, a single-service build tags koladei/frees3:<tag>."
      echo "For multi-service Docker Hub builds, tags become koladei/frees3-<service>:<tag>."
      echo ""
      echo "Options:"
      echo "  --registry      Container registry host or Docker repository base (default: taviatech.azurecr.io)"
      echo "  --service       Comma-separated service names or * for all (default: *)"
      echo "  --platform      Select image platform(s): all, amd64, or arm64 (default: all)"
      echo "  --deploy        Deploy services using docker-compose after push (optional path)"
      echo "  --no-pull       Skip 'git pull' at the start"
      echo "  --no-push       Build images locally but skip pushing to registry"
      echo "  --only-push     Skip builds and only push existing local images"
      echo "  --production    Use production tags (latest, latest-arm) instead of dev tags"
      exit 0
      ;;
    --deploy)
      # Allow --deploy with an optional value. If next token is empty or a flag, use default
      if [[ -z "$2" || "$2" == --* ]]; then
        deploy="./docker-compose.yml"
        shift 1
      else
        deploy="$2"
        shift 2
      fi
      ;;
    --no-pull)
      no_pull=1
      shift 1
      ;;
    --no-push)
      no_push=1
      shift 1
      ;;
    --only-push)
      only_push=1
      shift 1
      ;;
    --production)
      production=1
      shift 1
      ;;
    *)
      echo "Unknown option: $1"
      echo "Use --help for usage."
      exit 1
      ;;
  esac
done

# Normalize and validate platform selection
platforms=$(echo "$platforms" | tr '[:upper:]' '[:lower:]')
case "$platforms" in
  all)
    selected_platforms=("amd64" "arm64")
    ;;
  amd64|arm64)
    selected_platforms=("$platforms")
    ;;
  *)
    echo "❌ Invalid value for --platform: '$platforms'. Allowed values: all, amd64, arm64." >&2
    exit 1
    ;;
esac

get_image_tag() {
  local platform="$1"

  if [[ "$platform" == "arm64" ]]; then
    if [[ -n "$production" ]]; then
      echo "latest-arm"
    else
      echo "latest-arm-dev"
    fi
  else
    if [[ -n "$production" ]]; then
      echo "latest"
    else
      echo "latest-dev"
    fi
  fi
}

slugify_service_name() {
  local service="$1"
  echo "$service" | tr '[:upper:]' '[:lower:]'
}

is_registry_host() {
  local registry_value="$1"
  [[ "$registry_value" == *.* || "$registry_value" == *:* || "$registry_value" == "localhost" ]]
}

get_image_repository() {
  local service="$1"
  local service_slug
  service_slug="$(slugify_service_name "$service")"

  if is_registry_host "$registry"; then
    echo "$registry/$service_slug"
    return
  fi

  if [[ "${#services[@]}" -eq 1 ]]; then
    echo "$registry"
    return
  fi

  echo "$registry-$service_slug"
}

# All available microservices (service directories with Dockerfile)
# Update this list as you add more services
all_services=(
  "App_Gateway"
  "App_Auth"
  "App_Storage"
  "App_Contract"
)

# Determine target services
if [[ "$services_arg" == "*" ]]; then
  services=("${all_services[@]}")
else
  IFS=',' read -ra services <<< "$services_arg"
fi

# Go into project root
cd "$(dirname "$0")"
repo_root="$(pwd)"

cleanup() {
  true
}

trap cleanup EXIT

# Validate incompatible flags
if [[ -n "$only_push" && -n "$no_push" ]]; then
  echo "❌ Cannot use --only-push together with --no-push" >&2
  exit 1
fi

# Update repository to latest from remote before building/pushing
if [[ -z "$only_push" ]]; then
  echo "🔄 Updating repository (git pull) in $repo_root"
  if [[ -z "$no_pull" ]]; then
    git -C "$repo_root" pull
  else
    echo "ℹ️  Skipping git pull because --no-pull was passed"
  fi
fi

# Build each of the services
for service in "${services[@]}"; do
  if [[ ! -d "$service" ]]; then
    echo "⚠️  Skipping '$service': directory not found"
    continue
  fi

  if [[ ! -f "$service/Dockerfile" ]]; then
    echo "⚠️  Skipping '$service': Dockerfile not found"
    continue
  fi

  # Push-only mode: skip building, attempt to push existing local images
  if [[ -n "$only_push" ]]; then
    echo "📤 Only-push mode: pushing existing local images for $service"
    for platform in "${selected_platforms[@]}"; do
      tag="$(get_image_tag "$platform")"
      image_repository="$(get_image_repository "$service")"
      service_slug="$(slugify_service_name "$service")"
      target_tag="$image_repository:$tag"
      local_tag="$service_slug:$tag"

      if docker image inspect "$target_tag" >/dev/null 2>&1; then
        echo "🔁 Pushing existing local image $target_tag"
        docker push "$target_tag"
      elif docker image inspect "$local_tag" >/dev/null 2>&1; then
        echo "🔁 Tagging $local_tag -> $target_tag and pushing"
        docker tag "$local_tag" "$target_tag"
        docker push "$target_tag"
      else
        echo "⚠️  Local image not found: $local_tag or $target_tag. Skipping platform '$platform'."
      fi
    done

    echo "✅ Pushed images for $service"

    # Deploy if requested
    if [[ -n "$deploy" ]]; then
      if [[ ! "$deploy" = /* ]]; then
        compose_path="$repo_root/$deploy"
      else
        compose_path="$deploy"
      fi

      if [[ ! -f "$compose_path" ]]; then
        echo "⚠️  Deploy requested but compose file not found at: $compose_path"
      else
        echo "🚀 Deploying $service using compose file at $compose_path"
        docker compose -f "$compose_path" up -d "$service"
        echo "✅ Deployed $service via docker compose"
      fi
    fi

    continue
  fi

  echo "🚀 Building service: $service"

  # For .NET projects: dotnet restore and build are implicit in docker build
  # We validate the .csproj exists first
  csproj_file=$(find "$service" -maxdepth 1 -name "*.csproj" | head -1)
  if [[ -z "$csproj_file" ]]; then
    echo "❌ No .csproj found in $service directory"
    exit 1
  fi

  echo "📦 Found project: $csproj_file"

  # Build & push or build-only depending on flags
  if [[ -n "$no_push" ]]; then
    for platform in "${selected_platforms[@]}"; do
      image_tag="$(get_image_tag "$platform")"
      image_repository="$(get_image_repository "$service")"
      target_tag="$image_repository:$image_tag"

      echo "🐋 Building $platform image locally (skip push)..."
      docker buildx build \
        --platform="linux/$platform" \
        -t "$target_tag" \
        -f "$service/Dockerfile" \
        "$repo_root" --load
    done
    echo "ℹ️  Skipped pushing images because --no-push was provided."
  else
    for platform in "${selected_platforms[@]}"; do
      image_tag="$(get_image_tag "$platform")"
      image_repository="$(get_image_repository "$service")"
      target_tag="$image_repository:$image_tag"

      echo "🐋 Building and pushing $platform image..."
      docker buildx build \
        --platform="linux/$platform" \
        -t "$target_tag" \
        -f "$service/Dockerfile" \
        "$repo_root" --push
    done
  fi

  echo "✅ Finished building $service"

  # If deploy option provided, attempt to run docker compose to bring up the service
  if [[ -n "$deploy" ]]; then
    if [[ ! "$deploy" = /* ]]; then
      compose_path="$repo_root/$deploy"
    else
      compose_path="$deploy"
    fi

    if [[ ! -f "$compose_path" ]]; then
      echo "⚠️  Deploy requested but compose file not found at: $compose_path"
    else
      echo "🚀 Deploying $service using compose file at $compose_path"
      docker compose -f "$compose_path" up -d "$service"
      echo "✅ Deployed $service via docker compose"
    fi
  fi
done

if [[ -n "$no_push" ]]; then
  echo "🎉 Selected services built locally. Images were not pushed to $registry."
else
  echo "🎉 Selected services built and pushed successfully to $registry!"
fi
