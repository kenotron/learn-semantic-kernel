# Web Research Agent

This directory contains all components related to the Web Research Agent, a specialized AI assistant for conducting comprehensive web research.

## Components

### Core Agent
- **`WebResearchAgent.cs`** - The main agent implementation that orchestrates web research using multiple plugins

### Plugins
- **`WebSearchPlugin.cs`** - Provides web search capabilities using SearxNG API, returns formatted Markdown lists of search results
- **`WebPageAnalysisPlugin.cs`** - Analyzes individual web pages, extracts content, finds related links, and assesses research value

## Namespace
All components are in the `MultiAgent.Agents.WebResearchAgent` namespace.

## Features

### WebSearchPlugin
- Search the web using SearxNG API
- Returns results as formatted Markdown bulleted lists
- Includes titles (as clickable links), content previews, publication dates, and sources
- Configurable result limits and search categories

### WebPageAnalysisPlugin
- Extract and clean web page content
- Convert HTML to readable Markdown format
- Find and categorize related links on the page
- Assess research value with AI-powered relevance scoring
- Provide recommendations on whether to continue analyzing sources
- Smart filtering of non-content links (social media, login pages, etc.)

### Agent Capabilities
- Comprehensive research strategy combining broad search and deep analysis
- Intelligent source prioritization based on research value assessments
- Efficient crawling with depth limits to avoid excessive analysis
- Source attribution and conflict identification
- Structured reporting with clear organization

## Usage
The agent is designed to:
1. Start with broad web searches using `SearchWebAsync`
2. Analyze promising pages using `AnalyzePageAsync` 
3. Follow high-value related links for deeper insights
4. Provide comprehensive, well-structured research summaries

The agent prioritizes authoritative sources (.edu, .gov, .org domains) and focuses on content with high research value assessments.
