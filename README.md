# Preconditions

## Vector indexes configuration in Mongo Atlas

		{
		  "fields": [
			{
			  "numDimensions": 1536,
			  "path": "embedding",
			  "similarity": "cosine",
			  "type": "vector"
			},
			{
			  "path": "repo",
			  "type": "filter"
			},
			{
			  "path": "team",
			  "type": "filter"
			},
			{
			  "path": "name",
			  "type": "filter"
			},
			{
			  "numDimensions": 1536,
			  "path": "docsEmbedding",
			  "similarity": "cosine",
			  "type": "vector"
			}
		  ]
		}

that defines 2 vectors fields: 

* `embedding` - consists of general application details

Includes the following fields: `repo` (github link), `name` (application name), `team`, `summary`, `owners` (codeowners), `responsibilities`, `tags`, `dependencies`
	
* `docsEmbedding` - consists of summary for found documentation

# Application components

## MongoDB Atlas

Represents a vector storage for RAG.

## MCP server

Provides RAG functionality to AI models.

## ReportAISummary Mcp Client

Provides a way to test MCP server locally.

# How to use RAG

1. Run API application via `Scalar` UI.
2. Run `ReportAISummary.Mcp.Client` shell based application as MCP client.
3. Attach created MCP server to any AI aware env like cursor AI, Visual Studio, Github copilot and so on.

# Supported Functionality

1. Repository Parsing: Automatically parses GitHub repositories listed in supported-repos.json and stores the extracted data in vector storage for efficient semantic querying.

2. MCP Integration: Provides Model Context Protocol (MCP)–based endpoints to ask questions and retrieve insights from the previously indexed repository data.

# Future plans

1. Make indexing github repository data fully async.
2. Make RAG querying more precise including better `score` values.
3. Clarify components and env. Is MongoDB the best option here? Deploy to target ecosystem.
4. Add tests.