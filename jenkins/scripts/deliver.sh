#!/usr/bin/env sh

set -e

if [ -z "${DOCKER_IMAGE:-}" ]; then
	echo 'DOCKER_IMAGE is required, for example: mpsean/hummingbird-api' >&2
	exit 1
fi

if [ -z "${DOCKERHUB_USERNAME:-}" ] || [ -z "${DOCKERHUB_TOKEN:-}" ]; then
	echo 'DOCKERHUB_USERNAME and DOCKERHUB_TOKEN are required for Docker Hub login.' >&2
	exit 1
fi

IMAGE_TAG="${IMAGE_TAG:-${BUILD_NUMBER:-latest}}"

echo "Logging in to Docker Hub as ${DOCKERHUB_USERNAME}"
echo "${DOCKERHUB_TOKEN}" | docker login -u "${DOCKERHUB_USERNAME}" --password-stdin

echo "Pushing ${DOCKER_IMAGE}:${IMAGE_TAG}"
docker push "${DOCKER_IMAGE}:${IMAGE_TAG}"

echo "Pushing ${DOCKER_IMAGE}:latest"
docker push "${DOCKER_IMAGE}:latest"

docker logout
echo 'Docker image delivery completed.'
