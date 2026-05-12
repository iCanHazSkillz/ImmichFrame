---
sidebar_position: 2
---

# 🐋 Docker Setup [Docker Compose]

This guide shows how to start **ImmichFrame** using Docker Compose.

---

## Configuration Files

:::tip Recommended
For most users, the `Settings.yml` setup is easier to read and modify.
:::

Example configuration files:

- [`Settings.yml` example][example-yaml]
- [`Settings.json` example][example-json]
- [`.env` example][example-env]

---

## Docker Compose Example

:::warning Important
If using yaml or json settings, replace `PATH/TO/CONFIG` with the actual path to your config folder containing the settings file!
:::

```yaml
name: immichframe
services:
  immichframe:
    container_name: immichframe
    image: ghcr.io/immichframe/immichframe:latest
    restart: on-failure
    volumes:
      - PATH/TO/CONFIG:/app/Config
      # Optional for .env users: lets /admin/settings sync supported values back to .env.
      # If you use this, keep env_file pointing at the same host .env file.
      # - ./.env:/app/Config/.env
    ports:
      - "8080:8080"
    # env_file:
    #   - .env
    environment:
      TZ: "Europe/Berlin"
      # IMMICHFRAME_ENV_FILE_PATH: "/app/Config/.env"
```

External `.env` edits are imported on container restart. Settings saved in `/admin/settings` are written back to the mounted `.env` file immediately for fields supported by both the flat `.env` format and the UI. The mounted `.env` file must be writable by the container user.

[github-root]: https://github.com/immichframe/ImmichFrame/blob/main
[example-json]: https://github.com/immichframe/ImmichFrame/blob/main/docker/Settings.example.json
[example-yaml]: https://github.com/immichframe/ImmichFrame/blob/main/docker/Settings.example.yml
[example-env]: https://github.com/immichframe/ImmichFrame/blob/main/docker/example.env
