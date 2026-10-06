-- Run ONCE as a PostgreSQL administrator (e.g. postgres), connected to the "postgres" database:
--   psql -U postgres -h <host> -d postgres -v owner_pw="<owner password>" -v app_pw="<app password>" -f 01-create-roles-and-database.sql
-- Two roles, neither is a superuser:
--   cyberlms_owner : owns the database and applies schema migrations (DDL). Used by the DBA only, never by the website.
--   cyberlms_app   : the website's runtime identity. Data access only (see 03-grants.sql); cannot change the schema.
CREATE ROLE cyberlms_owner LOGIN PASSWORD :'owner_pw' NOSUPERUSER NOCREATEDB NOCREATEROLE;
CREATE ROLE cyberlms_app   LOGIN PASSWORD :'app_pw'   NOSUPERUSER NOCREATEDB NOCREATEROLE;
CREATE DATABASE cyberlms OWNER cyberlms_owner ENCODING 'UTF8' TEMPLATE template0;
REVOKE ALL ON DATABASE cyberlms FROM PUBLIC;
GRANT CONNECT ON DATABASE cyberlms TO cyberlms_app;
