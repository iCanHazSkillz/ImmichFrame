## 🔙 Back
Go back to the [Full Readme](/README.md)

## 🌐 ImmichFrame Web
- [🔙 Back](#-back)
- [🌐 ImmichFrame Web](#-immichframe-web)
- [✨ Demo](#-demo)
- [🔧 Installation](#-installation)
- [🐋 Docker Compose](#-docker-compose)
  - [Docker Compose with environment variables](#docker-compose-with-environment-variables)
  - [Docker Compose with Settings.json](#docker-compose-with-settingsjson)
  - [Docker Compose with env file](#docker-compose-with-env-file)
- [⚙️ Configuration](#️-configuration)
- [🆘 Help](#-help)

## ✨ Demo
![ImmichFrame Web](/design/demo/web_demo.png)

## 🔧 Installation
ImmichFrame Web is installed via [Docker 🐋](#-docker-compose)

## 🐋 Docker Compose
### Docker Compose with environment variables

> [!NOTE]  
> Not every setting is needed. Only configure what you need.
> Runtime-editable settings can now be managed later from `/admin/settings`, so most installs only need the bootstrap values shown below.

```yaml
name: immichframe
services:
  immichframe:
    container_name: immichframe
    image: ghcr.io/immichframe/immichframe:latest
    restart: on-failure
    ports:
      - "8080:8080"
    environment:
      TZ: "Europe/Berlin"
      ImmichServerUrl: "URL"
      ApiKey: "KEY"
      # IMMICHFRAME_AUTH_BASIC_ADMIN_USER: "admin"
      # IMMICHFRAME_AUTH_BASIC_ADMIN_HASH: "$$apr1$$..."
      # AuthenticationSecret: ""
```

### Docker Compose with Settings.json

An example of the Settings.json can be found [here](/docker/Settings.example.json).

> [!IMPORTANT]  
> Change `PATH/TO/CONFIG` to the correct path!

```yaml
name: immichframe
services:
  immichframe:
    container_name: immichframe
    image: ghcr.io/immichframe/immichframe:latest
    restart: on-failure
    volumes:
      - PATH/TO/CONFIG:/app/Config
    ports:
      - "8080:8080"
    environment:
      TZ: "Europe/Berlin"
```

### Docker Compose with env file

An example of the .env can be found [here](/docker/example.env).

```yaml
name: immichframe
services:
  immichframe:
    container_name: immichframe
    image: ghcr.io/immichframe/immichframe:latest
    restart: on-failure
    ports:
      - "8080:8080"
    env_file:
      - .env
    environment:
      TZ: "Europe/Berlin"
```

## ⚙️ Configuration

For more information, read [here](/README.md#configuration).

The frame UI and the admin dashboard are both available on `http://HOST:8080`, with the admin dashboard at `http://HOST:8080/admin`.

Bootstrap config is still loaded from `Settings.json`, `Settings.yml`, `Settings.yaml`, or environment variables at startup. After the app starts, runtime-editable settings are stored separately in `App_Data/admin-settings.json` and managed from `http://HOST:8080/admin/settings`.

For new installs, the bootstrap config typically only needs:

- `ImmichServerUrl`
- `ApiKey` or `ApiKeyFile`
- `IMMICHFRAME_AUTH_BASIC_*` values for admin login
- Optional `AuthenticationSecret` if you use bearer protection for frames

To enable the admin login page, add at least one matching `IMMICHFRAME_AUTH_BASIC_*_USER` and `IMMICHFRAME_AUTH_BASIC_*_HASH` pair to your environment or `.env` file. These env values remain the source of truth for admin users, and the `/admin` page signs in against them with a normal session cookie.

If `/admin` is reachable outside a trusted local network, only expose it over HTTPS or behind a TLS-terminating reverse proxy such as Nginx or Traefik. The admin login uses environment-backed credentials and session cookies, and both can be intercepted if the endpoint is exposed over plain HTTP.

## 🆘 Help

[Discord Channel][support-url]


<!-- MARKDOWN LINKS & IMAGES -->
[support-url]: https://discord.com/channels/979116623879368755/1217843270244372480
