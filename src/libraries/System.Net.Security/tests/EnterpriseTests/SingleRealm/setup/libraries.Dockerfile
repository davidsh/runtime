# Builds a dotnet sdk base image which contains runtime-libraries bits compiled from source
ARG BUILD_BASE_IMAGE=mcr.microsoft.com/dotnet-buildtools/prereqs:rhel7_prereqs_2
ARG SDK_BASE_IMAGE=mcr.microsoft.com/dotnet/core/sdk:3.0.100-bionic

FROM $BUILD_BASE_IMAGE as librariesbuild

WORKDIR /repo
COPY . .

ARG CONFIGURATION=Debug
ARG BUILD_SCRIPT_NAME=libraries
RUN ./$BUILD_SCRIPT_NAME.sh -c $CONFIGURATION

FROM $SDK_BASE_IMAGE as target

ARG TESTHOST_LOCATION=/repo/artifacts/bin/testhost
ARG TFM=netcoreapp
ARG OS=Linux
ARG ARCH=x64
ARG CONFIGURATION=Debug

ARG LIBRARIES_SHARED_FRAMEWORK_NAME=Microsoft.NETCore.App
ARG SOURCE_LIBRARIES_VERSION=5.0.0
ARG TARGET_SHARED_FRAMEWORK=/usr/share/dotnet/shared
ARG TARGET_LIBRARIES_VERSION=3.0.0

COPY --from=librariesbuild \
    $TESTHOST_LOCATION/$TFM-$OS-$CONFIGURATION-$ARCH/shared/$LIBRARIES_SHARED_FRAMEWORK_NAME/$SOURCE_LIBRARIES_VERSION/* \
    $TARGET_SHARED_FRAMEWORK/$LIBRARIES_SHARED_FRAMEWORK_NAME/$TARGET_LIBRARIES_VERSION/
