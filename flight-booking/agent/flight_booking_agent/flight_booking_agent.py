"""SkyWave Airlines flight-booking agent.

A thin runtime wrapper around :class:`SyncfusionAgent` that wires up the
SkyWave A2UI design catalog at boot and serves the A2A protocol on
``http://127.0.0.1:10006`` for the bundled React client.

Run with::

    flight-booking
"""

from __future__ import annotations

import logging
import os

from syncfusion_a2ui_agent import SyncfusionAgent
from syncfusion_a2ui_agent.providers.base import AIProvider

_log = logging.getLogger("syncfusion_a2ui_agent")

DESIGNS_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "designs")

# Token budget for echoing one design envelope verbatim. The A2UI design
# JSONs are ~6-12 KB; 8000 completion tokens is a comfortable ceiling
# above the ~3-5K tokens the model actually emits per response.
_LLM_SPEED_KNOBS: dict[str, str | int] = {
    "max_completion_tokens": 8000,
}

_REQUIRED_ENV_VARS = (
    "OPENROUTER_API_KEY",
    "MODEL_NAME",
)


class FlightBookingAgent(SyncfusionAgent):
    """Echoes one of the three pre-defined SkyWave pages per request."""

    def __init__(self, model: str | AIProvider | type[AIProvider]) -> None:
        # Call parent init - it will call enable_skills() but we override it to be safe
        super().__init__(model=model)

        # Pre-load the full catalog once at boot. The hot path will swap
        # in a single design for clear-intent queries so the model only
        # ever sees one page.
        if not os.path.isdir(DESIGNS_DIR):
            raise FileNotFoundError(
                f"Designs directory not found: {DESIGNS_DIR!r}. "
                "Create it with stage1/stage2/stage3 .json files before "
                "starting the agent."
            )
        self.set_design(DESIGNS_DIR)

        # Optional local skill routing when SKILLS_PATH is set in .env.
        # Now that _engine is initialized, we can safely enable skills.
        skills_path = os.environ.get("SKILLS_PATH", "")
        if skills_path:
            registry = self.enable_skills(skills_path)
            skills = registry.all_skills()
            _log.info(
                "Loaded %d skill(s) from %s: %s",
                len(skills),
                skills_path,
                [s.name for s in skills],
            )
        else:
            _log.info(
                "SKILLS_PATH not set - skill routing disabled. "
                "Set SKILLS_PATH=<path> in your .env to enable it."
            )

    def enable_skills(self, skills_path: str | None = None) -> object:
        """Override to safely handle enable_skills during parent init.

        The parent __init__ calls enable_skills() before _engine is ready,
        so we defer actual skill loading until _engine exists.
        """
        if not hasattr(self, "_engine"):
            # Called from parent __init__ before the engine is ready — skip.
            return None
        return (
            super().enable_skills(skills_path)
            if skills_path
            else super().enable_skills()
        )

    def extend_system_prompt(self) -> str:
        """Append system prompt from system_prompt.md file.

        The catalog of pages is already injected by ``set_design`` on the
        base class. This method reads the instructional prompt from the
        system_prompt.md file that tells the model how to echo designs
        verbatim and handle the interactive booking flow.
        """
        prompt_path = os.path.join(
            os.path.dirname(os.path.abspath(__file__)), "system_prompt.md"
        )
        try:
            with open(prompt_path, "r", encoding="utf-8") as f:
                content = f.read()
                _log.info(
                    "Loaded system_prompt.md: %d chars from %s",
                    len(content),
                    prompt_path,
                )
                return content
        except FileNotFoundError:
            _log.warning(
                "system_prompt.md not found at %s; using fallback prompt.",
                prompt_path,
            )
            return (
                "You are the SkyWave Airlines flight-booking assistant. "
                "Echo the design above verbatim (same surfaceId, same components, "
                "same dataModel). Vary only the data values."
            )


def _require_env(env_path: str) -> None:
    """Raise a helpful error if ``.env`` or required vars are missing."""
    if not os.path.isfile(env_path):
        raise FileNotFoundError(
            f".env file not found at {env_path!r}. Copy .env.example to .env "
            "and fill in your OpenRouter credentials before starting the agent."
        )
    missing = [name for name in _REQUIRED_ENV_VARS if not os.environ.get(name)]
    if missing:
        raise OSError(
            "Missing required environment variables: "
            + ", ".join(missing)
            + ". Set them in your .env file or shell before starting the agent."
        )


def main() -> None:
    from dotenv import load_dotenv

    # Handle both direct execution and module execution
    try:
        from .custom_providers import OpenRouterProvider
    except ImportError:
        from custom_providers import OpenRouterProvider

    # Configure logging to show INFO level messages
    logging.basicConfig(
        level=logging.INFO,
        format="%(levelname)s: %(message)s",
    )
    _log.info("Starting FlightBookingAgent...")

    env_path = os.path.join(os.path.dirname(__file__), ".env")
    load_dotenv(env_path)
    _require_env(env_path)

    # Use OpenRouter provider with 100+ models
    provider = OpenRouterProvider(
        api_key=os.environ["OPENROUTER_API_KEY"],
        model=os.environ["MODEL_NAME"],
        base_url="https://openrouter.ai/api/v1",
        **_LLM_SPEED_KNOBS,
    )
    agent = FlightBookingAgent(model=provider)
    # A2A server - the bundled test-page.html POSTs to this URL.
    agent.serve(host="127.0.0.1", port=10006)


if __name__ == "__main__":
    main()
