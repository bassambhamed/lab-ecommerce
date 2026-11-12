-- Rôle de connexion en lecture seule pour explorer les bases via MCP.
--
-- À exécuter avec psql, en superutilisateur, sur le serveur PostgreSQL de l'AppHost :
--   psql -v ON_ERROR_STOP=1 -v mot_de_passe=<mot de passe> -U postgres -d postgres -f role-lecture-seule.sql
-- creer-role-lecture-seule.sh le fait pour vous, sans que le mot de passe apparaisse dans la
-- ligne de commande. Le script est rejouable : il recrée les droits et change le mot de passe.
--
-- Le contrôle d'accès est porté par la base : même si le serveur MCP est mal configuré
-- (mode unrestricted) ou contourné, ce rôle ne peut que lire.

\set ON_ERROR_STOP on

-- Le rôle : connexion, aucun attribut d'administration.
SELECT 'CREATE ROLE lecteur_mcp'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'lecteur_mcp') \gexec

ALTER ROLE lecteur_mcp WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS
  CONNECTION LIMIT 10 PASSWORD :'mot_de_passe';

-- Garde-fous de session : une requête d'exploration ne doit ni durer, ni bloquer l'application.
ALTER ROLE lecteur_mcp SET statement_timeout = '30s';
ALTER ROLE lecteur_mcp SET lock_timeout = '2s';
ALTER ROLE lecteur_mcp SET idle_in_transaction_session_timeout = '60s';

-- Contrôle affiché pour chaque base : les privilèges du rôle sur les tables existantes.
\set controle 'SELECT current_database() AS base, table_name AS "table", string_agg(privilege_type, '', '') AS droits FROM information_schema.role_table_grants WHERE grantee = ''lecteur_mcp'' GROUP BY 1, 2 ORDER BY 2;'

-- Les droits, base par base : connexion, usage du schéma, lecture des tables. Les tables
-- que les migrations créeront plus tard (propriétaire : postgres) seront lisibles aussi.
\connect catalogdb
GRANT CONNECT ON DATABASE catalogdb TO lecteur_mcp;
GRANT USAGE ON SCHEMA public TO lecteur_mcp;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO lecteur_mcp;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT SELECT ON TABLES TO lecteur_mcp;
:controle

\connect orderingdb
GRANT CONNECT ON DATABASE orderingdb TO lecteur_mcp;
GRANT USAGE ON SCHEMA public TO lecteur_mcp;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO lecteur_mcp;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT SELECT ON TABLES TO lecteur_mcp;
:controle
