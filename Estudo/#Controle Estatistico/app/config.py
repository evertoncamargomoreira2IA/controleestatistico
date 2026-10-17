from functools import lru_cache
import warnings

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    app_name: str = "QMS CEP"
    environment: str = "development"
    secret_key: str | None = None
    database_url: str = "sqlite:///./data/qms.db"
    session_max_age: int = 1800
    login_max_attempts: int = 5
    login_lock_minutes: int = 15
    password_min_length: int = 12
    cookie_secure: bool = False
    model_config = SettingsConfigDict(env_file=".env", extra="ignore", case_sensitive=False)

    def signing_key(self) -> str:
        if self.secret_key:
            return self.secret_key
        if self.environment.lower() not in {"dev", "development", "local"}:
            raise RuntimeError("SECRET_KEY é obrigatório fora do desenvolvimento")
        warnings.warn(
            "Using development SECRET_KEY fallback; set SECRET_KEY in .env.",
            RuntimeWarning,
            stacklevel=2,
        )
        return "dev-only-change-me-before-deployment-32chars!"


@lru_cache
def get_settings() -> Settings:
    return Settings()
