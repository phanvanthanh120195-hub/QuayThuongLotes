# Giai đoạn 1: Build ứng dụng bằng .NET 8 SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file csproj và khôi phục các gói NuGet
COPY ["LuckyDraw.Web.csproj", "./"]
RUN dotnet restore "LuckyDraw.Web.csproj"

# Copy toàn bộ mã nguồn và publish bản Release
COPY . .
RUN dotnet publish "LuckyDraw.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Giai đoạn 2: Chạy ứng dụng bằng ASP.NET Core 8 Runtime cho nhẹ
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 10000
ENTRYPOINT ["dotnet", "LuckyDraw.Web.dll"]