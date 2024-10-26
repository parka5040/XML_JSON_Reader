#!/bin/bash

CONFIGURATION=${1:-Release}
RUNTIME=${2:-all}

declare -A PUBLISH_PROFILES=(
    ["win-x64"]="Windows x64"
    ["linux-x64"]="Linux x64"
    ["osx-x64"]="macOS x64"
)

if [ "$RUNTIME" != "all" ]; then
    RUNTIMES=("$RUNTIME")
else
    RUNTIMES=("${!PUBLISH_PROFILES[@]}")
fi

for runtime in "${RUNTIMES[@]}"; do
    echo "Publishing for ${PUBLISH_PROFILES[$runtime]}..."
    
    OUTPUT_PATH="publish/$runtime"
    
    dotnet publish src/Parser.UI/Parser.UI.csproj \
        --configuration "$CONFIGURATION" \
        --runtime "$runtime" \
        --self-contained true \
        --output "$OUTPUT_PATH" \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -p:PublishReadyToRun=true \
        -p:IncludeNativeLibrariesForSelfExtract=true
    
    if [ $? -eq 0 ]; then
        echo "Successfully published to $OUTPUT_PATH"
    else
        echo "Error: Failed to publish for ${PUBLISH_PROFILES[$runtime]}"
        exit 1
    fi
done

echo "Publishing complete!"