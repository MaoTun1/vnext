#!/bin/bash

# Default to development mode
ENV=${1:-inf}

docker network create bbt-development

if [ "$ENV" == "stage" ]; then
  echo "Starting in STAGE mode (no debugger)"
  docker-compose -f docker-compose.stage.yml up --build
elif [ "$ENV" == "dev" ]; then
  echo "Starting in DEVELOPMENT debugging mode (with debugger)"
  docker-compose -f docker-compose.dev.yml up --build
else
  echo "Starting in DEVELOPMENT mode (infrastructure)"
  docker-compose up --build
fi
