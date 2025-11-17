.PHONY: build release clean test info help

LINUX_BUILD_SCRIPT := scripts/build-linux.sh

help: ## Show this help message
	@echo "VibeProxy for Linux"
	@echo ""
	@echo "Available targets:"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-15s\033[0m %s\n", $$1, $$2}'

build: ## Build Linux artifacts in Debug configuration
	@bash "$(LINUX_BUILD_SCRIPT)" Debug

release: ## Build Linux artifacts in Release configuration
	@bash "$(LINUX_BUILD_SCRIPT)" Release

clean: ## Clean build artifacts
	@echo "🧹 Cleaning..."
	@dotnet clean
	@rm -rf out
	@echo "✅ Clean complete"

test: ## Run tests
	@echo "🧪 Running tests..."
	@dotnet test
	@echo "✅ Tests complete"

info: ## Show project information
	@echo "Project: VibeProxy for Linux"
	@echo "Language: C# / .NET 8.0"
	@echo "Platform: Linux (x64 / ARM64)"
	@echo "UI Framework: Avalonia"
	@echo ""
	@echo "Files:"
	@find src -name "*.cs" -exec wc -l {} + 2>/dev/null | tail -1 | awk '{print "  C# code: " $$1 " lines"}' || echo "  C# code: (run from project root)"
	@echo "  Documentation: 4 files"
	@echo ""
	@echo "Structure:"
	@tree -L 3 src tests || echo "  (install 'tree' for better output)"

# Shortcuts
all: release ## Same as 'release'
