import { Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { AddApiPage } from './pages/AddApiPage';
import { ApiDetailPage } from './pages/ApiDetailPage';
import { CatalogPage } from './pages/CatalogPage';

export default function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<CatalogPage />} />
        <Route path="/add" element={<AddApiPage />} />
        <Route path="/apis/:id" element={<ApiDetailPage />} />
      </Routes>
    </Layout>
  );
}
